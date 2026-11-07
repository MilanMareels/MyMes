using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyMES.PluginSystem;

namespace MyMES.Extensions.Sample;

/// <summary>
/// A worked example of a third-party extension.
/// </summary>
/// <remarks>
/// Demonstrates the <strong>Extend</strong> mechanism (its own endpoint) and shows the
/// <strong>Replace</strong> seam as a commented example. The <strong>Decorate</strong> mechanism is
/// shown in <c>ProductLoggerPlugin</c>, which wraps <c>IProductService</c>.
/// Disabled unless <c>Extensions:Sample:Enabled</c> is true, so a stock build is unaffected.
/// </remarks>
public sealed class SampleExtensionPlugin : IMyMesPlugin, IEndpointModule
{
    private bool _enabled;

    public string Name => "Sample extension";

    public string Version => "1.0.0";

    public string CompatiblePlatformVersion => "0.1";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        _enabled = configuration.GetValue<bool>("Extensions:Sample:Enabled");
        if (!_enabled)
        {
            return;
        }

        // Replace: this is how a customer would substitute their own implementation outright.
        // services.ReplaceService<IProductService, CustomerProductService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        if (!_enabled)
        {
            return;
        }

        // Extend: a route the platform knows nothing about.
        endpoints.MapGet("/extensions/sample/status", () => Results.Ok(new
            {
                Extension = "Sample extension",
                Version = "1.0.0",
                Note = "Loaded from extensions/MyMES.Extensions.Sample."
            }))
            .WithTags("Extensions")
            .WithName("GetSampleExtensionStatus")
            .WithSummary("Proves that an extension can add its own endpoint.")
            .RequireAuthorization();
    }
}
