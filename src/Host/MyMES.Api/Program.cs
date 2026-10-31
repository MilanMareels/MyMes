using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MyMES.Abstractions.Security;
using MyMES.Api;
using MyMES.Api.Endpoints;
using MyMES.Api.ExceptionHandling;
using MyMES.Api.Extensions;
using MyMES.Api.Realtime;
using MyMES.Api.Security;
using MyMES.Application;
using MyMES.Identity;
using MyMES.Infrastructure;
using MyMES.Messaging;
using MyMES.Persistence;
using MyMES.PluginSystem;
using Serilog;
using Serilog.Events;

// A bootstrap logger captures failures that happen before the host is built; without it a
// configuration mistake shows up as a silent non-zero exit.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "MyMES.Api")
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture, outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

    var platformVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(2) ?? "0.1";

    // ---- Options -----------------------------------------------------------
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
    var signingKey = jwtOptions.ResolveKey(builder.Environment.IsDevelopment(), out var keyWasGenerated);
    jwtOptions.Key = signingKey;

    // Registered as the resolved instance rather than bound again from configuration. Binding it
    // twice would let the validation parameters below and the options the token service reads
    // drift apart, and a token signed for one issuer then fails validation against the other.
    builder.Services.AddSingleton<IOptions<JwtOptions>>(Options.Create(jwtOptions));

    if (keyWasGenerated)
    {
        Log.Warning(
            "No {Section}:Key configured; using a key generated for this process. "
            + "Every token becomes invalid when the API restarts.",
            JwtOptions.SectionName);
    }

    // ---- Core services ------------------------------------------------------
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddMyMesIdentity();
    builder.Services.AddMyMesAuthentication(jwtOptions, signingKey);
    builder.Services.AddMyMesVersioning();
    builder.Services.AddMyMesSwagger();

    // The API knows who is calling and can push to clients, so it replaces the two defaults the
    // infrastructure registered for hosts that cannot.
    builder.Services.AddHttpContextAccessor();
    builder.Services.Replace(ServiceDescriptor.Scoped<ICurrentUser, HttpCurrentUser>());
    builder.Services.Replace(ServiceDescriptor.Scoped<IRealtimeNotifier, SignalRRealtimeNotifier>());

    builder.Services.AddSignalR();
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // Migrations and identity seeding run as part of host start-up, before the first request.
    builder.Services.AddHostedService<StartupInitialiser>();

    builder.Services.Configure<JsonOptions>(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

        // Enums travel as names, not ordinals. A status of "InProgress" survives a member being
        // inserted into the enum; a status of 2 does not, and it is unreadable in a log or a
        // Swagger example besides.
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    // ---- Modules and plugins ------------------------------------------------
    // The permission catalogue is created before the modules run so that a module can add its own
    // permissions, and before the policies are built so each one gets a policy.
    var permissionCatalog = new PermissionCatalog();
    builder.Services.AddSingleton<IPermissionCatalog>(permissionCatalog);

    // Module discovery runs before the host exists, so it needs a logger of its own.
    using var startupLoggerFactory = LoggerFactory.Create(logging => logging.AddSerilog(Log.Logger));

    builder.Services.AddMyMesModules(
        builder.Configuration,
        platformVersion,
        startupLoggerFactory.CreateLogger("MyMES.Modules"),
        typeof(MyMES.Modules.Machines.MachinesModule).Assembly,
        typeof(MyMES.Modules.Production.ProductionModule).Assembly,
        typeof(MyMES.Modules.Quality.QualityModule).Assembly,
        typeof(MyMES.Modules.Inventory.InventoryModule).Assembly,
        typeof(MyMES.Extensions.Sample.SampleExtensionPlugin).Assembly);

    builder.Services.AddMyMesAuthorization(permissionCatalog);

    // ---- Health -------------------------------------------------------------
    builder.Services
        .AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("MyMes")
            ?? throw new InvalidOperationException("Connection string 'MyMes' is not configured."),
            name: "sql-server",
            tags: ["ready"]);

    // ---- CORS ---------------------------------------------------------------
    var allowedOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    if (allowedOrigins.Length > 0)
    {
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // Required for the SignalR handshake to carry its credentials.
            .AllowCredentials()));
    }

    var app = builder.Build();

    // ---- Pipeline -----------------------------------------------------------
    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    if (allowedOrigins.Length > 0)
    {
        app.UseCors();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "MyMES API v1");
            options.DocumentTitle = "MyMES API";
        });
    }

    // ---- Routes -------------------------------------------------------------
    ApiVersionSet versionSet = app.NewApiVersionSet()
        .HasApiVersion(new ApiVersion(1, 0))
        .ReportApiVersions()
        .Build();

    var api = app.MapGroup("/api/v1").WithApiVersionSet(versionSet);

    api.MapAuthEndpoints();
    api.MapUserEndpoints();
    api.MapPlatformEndpoints();

    // Every module and plugin contributes its own routes under the same versioned group.
    foreach (var endpointModule in app.Services.GetServices<IEndpointModule>())
    {
        endpointModule.MapEndpoints(api);
    }

    app.MapHub<ShopFloorHub>("/hubs/shop-floor");

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        // Liveness must not depend on the database: a failing dependency should not make the
        // orchestrator kill a process that is otherwise healthy.
        Predicate = _ => false
    }).AllowAnonymous();

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = static async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                status = report.Status.ToString(),
                totalDurationMs = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    // Exception detail is deliberately omitted: this endpoint is reachable
                    // without authentication so an orchestrator can poll it.
                    durationMs = e.Value.Duration.TotalMilliseconds
                })
            });
        }
    }).AllowAnonymous();

    app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous().ExcludeFromDescription();

    Log.Information("MyMES API {Version} starting in {Environment}.", platformVersion, app.Environment.EnvironmentName);

    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "MyMES API terminated unexpectedly.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}


/// <summary>
/// Makes the implicit entry-point class reachable from the integration tests.
/// </summary>
/// <remarks>
/// Top-level statements generate an internal <c>Program</c>, which
/// <c>WebApplicationFactory&lt;T&gt;</c> cannot use as its marker type. Declaring the partial
/// here is the documented way to expose it, and is preferable to opening the whole assembly up
/// with <c>InternalsVisibleTo</c>.
/// </remarks>
public partial class Program;


