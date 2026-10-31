using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyMES.PluginSystem;

/// <summary>
/// A third-party extension to the platform.
/// </summary>
/// <remarks>
/// <para>
/// A plugin differs from a module only in who wrote it. Modules ship with the product; plugins
/// come from a customer or an integrator. They share the same registration seam, which is what
/// lets an extension do anything a built-in module can: replace a service, wrap one, add
/// endpoints, or run its own background work.
/// </para>
/// <para>
/// <strong>Security.</strong> A plugin loaded in-process runs with the full permissions of the
/// host: it can read every connection string, open sockets and touch the file system.
/// <c>AssemblyLoadContext</c> isolates <em>assembly versions</em>, not privileges, and is not a
/// sandbox. Treat installing a plugin as equivalent to deploying code you wrote, and run genuinely
/// untrusted extensions in a separate process or container that talks to MyMES over HTTP. See
/// <c>docs/plugins/getting-started.md</c>.
/// </para>
/// </remarks>
public interface IMyMesPlugin
{
    /// <summary>Stable identifier, shown in the platform inventory.</summary>
    string Name { get; }

    /// <summary>Plugin version, for diagnostics and support.</summary>
    string Version { get; }

    /// <summary>
    /// Minimum platform version this plugin was built against, for example <c>0.1</c>. The host
    /// logs a warning when it does not match, rather than refusing to start.
    /// </summary>
    string CompatiblePlatformVersion { get; }

    /// <summary>
    /// Registers the plugin's services.
    /// </summary>
    /// <remarks>
    /// Runs after every built-in module, so <c>services.Replace(...)</c> and
    /// <c>services.Decorate(...)</c> see the defaults they are meant to override.
    /// </remarks>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
