using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyMES.Abstractions.Modules;

/// <summary>
/// A functional area of the MES that owns its own services and registers them with the host.
/// </summary>
/// <remarks>
/// A module is the unit of extension in the modular monolith. It may depend on the application
/// layer and on other modules' <em>contracts</em>, but never on another module's implementation.
/// The host discovers every implementation at start-up and calls
/// <see cref="ConfigureServices"/> once, in <see cref="Order"/> sequence.
/// </remarks>
public interface IMyMesModule
{
    /// <summary>Stable identifier, used in logs, diagnostics and the module inventory endpoint.</summary>
    string Name { get; }

    /// <summary>
    /// Registration order. Lower values run first; modules that register defaults other modules
    /// may replace or decorate should use a lower number. Defaults to 100 for ordinary modules.
    /// </summary>
    int Order => 100;

    /// <summary>Registers everything this module contributes to the host container.</summary>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
