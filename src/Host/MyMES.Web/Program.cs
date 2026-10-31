using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using MyMES.Abstractions.Security;
using MyMES.Localization;
using MyMES.Web.Components;
using MyMES.Web.Services;
using Serilog;
using Serilog.Events;

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
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "MyMES.Web")
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture, outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

    // Interactivity is opted into per component rather than globally: the sign-in page has to run
    // as static server rendering so it can write the authentication cookie through HttpContext.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddAuthorization();
    builder.Services.AddScoped<ICurrentUser, WebCurrentUser>();

    builder.Services.Configure<LocalizationOptions>(
        builder.Configuration.GetSection(LocalizationOptions.SectionName));
    builder.Services.AddMyMesLocalization();

    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.AccessDeniedPath = "/access-denied";
            options.Cookie.Name = "MyMES.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            // Over plain HTTP in a container the cookie still has to be sent; the reverse proxy
            // terminates TLS. SameSite=Lax plus HttpOnly is the meaningful protection here.
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.SlidingExpiration = false;
        });

    // The UI reaches data only through the API.
    var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Api:BaseUrl is not configured.");

    builder.Services.AddTransient<ApiTokenHandler>();
    builder.Services.AddHttpClient<MyMesApiClient>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl.EndsWith('/') ? apiBaseUrl : apiBaseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(
                builder.Configuration.GetValue<int?>("Api:TimeoutSeconds") ?? 30);
        })
        .AddHttpMessageHandler<ApiTokenHandler>();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStaticFiles();
    app.UseAntiforgery();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Sign-out needs HttpContext, so it is an endpoint rather than a component action.
    app.MapPost("/logout", async (HttpContext context) =>
    {
        await WebSignIn.SignOutAsync(context);
        return Results.Redirect("/login");
    });

    app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();

    Log.Information("MyMES Web starting in {Environment}, API at {ApiBaseUrl}.",
        app.Environment.EnvironmentName, apiBaseUrl);

    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "MyMES Web terminated unexpectedly.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

