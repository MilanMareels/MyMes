using System.Globalization;
using MyMES.MachineService;
using MyMES.MachineService.Api;
using MyMES.MachineService.Configuration;
using MyMES.MachineService.Drivers;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "MyMES.MachineService")
        .WriteTo.Console(
            formatProvider: CultureInfo.InvariantCulture,
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

    builder.Services.Configure<MachineServiceOptions>(
        builder.Configuration.GetSection(MachineServiceOptions.SectionName));

    var options = builder.Configuration
        .GetSection(MachineServiceOptions.SectionName)
        .Get<MachineServiceOptions>() ?? new MachineServiceOptions();

    // Drivers are registered as a collection: adding a protocol means adding one registration,
    // and a plugin can contribute its own driver the same way.
    builder.Services.AddSingleton<IMachineDriver, SimulatedMachineDriver>();

    builder.Services.AddSingleton<MachineHealthState>();
    builder.Services.AddHttpClient<IMesApiGateway, MesApiGateway>(client =>
    {
        client.BaseAddress = new Uri(options.ApiBaseUrl.EndsWith('/') ? options.ApiBaseUrl : options.ApiBaseUrl + "/");
        client.Timeout = TimeSpan.FromSeconds(15);
    });

    builder.Services
        .AddHealthChecks()
        .AddCheck<MachinePollingHealthCheck>("machine-polling");

    builder.Services.AddHostedService<MachinePollingService>();

    var host = builder.Build();

    Log.Information("MyMES MachineService starting, MES API at {ApiBaseUrl}.", options.ApiBaseUrl);

    await host.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "MyMES MachineService terminated unexpectedly.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
