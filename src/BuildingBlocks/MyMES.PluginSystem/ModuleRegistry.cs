using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMES.Abstractions.Modules;

namespace MyMES.PluginSystem;

/// <summary>What the host loaded at start-up, available for diagnostics at run time.</summary>
public sealed record LoadedModule(string Name, int Order, string AssemblyName);

/// <summary>The modules and plugins this process is running with.</summary>
public interface IModuleRegistry
{
    IReadOnlyList<LoadedModule> Modules { get; }

    IReadOnlyList<string> Plugins { get; }
}

internal sealed class ModuleRegistry(IReadOnlyList<LoadedModule> modules, IReadOnlyList<string> plugins)
    : IModuleRegistry
{
    public IReadOnlyList<LoadedModule> Modules { get; } = modules;

    public IReadOnlyList<string> Plugins { get; } = plugins;
}

public static class ModuleRegistrationExtensions
{
    /// <summary>
    /// Discovers and registers every module and plugin in the given assemblies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Modules run first, in <see cref="IMyMesModule.Order"/> sequence, then plugins. That order
    /// is the whole point: by the time a plugin's <c>ConfigureServices</c> runs, every default it
    /// might want to replace or decorate is already registered.
    /// </para>
    /// <para>
    /// Discovery is over assemblies the host already references. Loading arbitrary DLLs from a
    /// directory is deliberately not done here; see <see cref="IMyMesPlugin"/> on why that is a
    /// deployment decision rather than a security boundary.
    /// </para>
    /// </remarks>
    /// <param name="services">Container being built.</param>
    /// <param name="configuration">Host configuration, handed to each module.</param>
    /// <param name="platformVersion">Version the host advertises, for compatibility warnings.</param>
    /// <param name="logger">Receives what was loaded and what was skipped.</param>
    /// <param name="assemblies">Assemblies to scan.</param>
    public static IServiceCollection AddMyMesModules(
        this IServiceCollection services,
        IConfiguration configuration,
        string platformVersion,
        ILogger logger,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        var distinct = assemblies.Where(a => a is not null).Distinct().ToArray();

        var modules = Instantiate<IMyMesModule>(distinct, logger)
            .OrderBy(m => m.Order)
            .ThenBy(m => m.Name, StringComparer.Ordinal)
            .ToList();

        var loaded = new List<LoadedModule>(modules.Count);

        foreach (var module in modules)
        {
            module.ConfigureServices(services, configuration);

            // A module that owns routes is registered as its own instance, so the host can ask the
            // container for every route contributor instead of knowing the module types.
            if (module is IEndpointModule endpointModule)
            {
                services.AddSingleton<IEndpointModule>(endpointModule);
            }

            loaded.Add(new LoadedModule(module.Name, module.Order, module.GetType().Assembly.GetName().Name ?? "?"));
            logger.LogInformation("Module {Module} registered (order {Order}).", module.Name, module.Order);
        }

        var plugins = Instantiate<IMyMesPlugin>(distinct, logger)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        var pluginNames = new List<string>(plugins.Count);

        foreach (var plugin in plugins)
        {
            if (!IsCompatible(plugin.CompatiblePlatformVersion, platformVersion))
            {
                logger.LogWarning(
                    "Plugin {Plugin} {Version} targets platform {Target} but this host is {Actual}. "
                    + "Loading it anyway; verify it behaves.",
                    plugin.Name,
                    plugin.Version,
                    plugin.CompatiblePlatformVersion,
                    platformVersion);
            }

            plugin.ConfigureServices(services, configuration);

            if (plugin is IEndpointModule endpointPlugin)
            {
                services.AddSingleton<IEndpointModule>(endpointPlugin);
            }

            pluginNames.Add($"{plugin.Name} {plugin.Version}");
            logger.LogInformation("Plugin {Plugin} {Version} registered.", plugin.Name, plugin.Version);
        }

        services.AddSingleton<IModuleRegistry>(new ModuleRegistry(loaded, pluginNames));

        return services;
    }

    /// <summary>
    /// Creates one instance of every public, concrete implementation of <typeparamref name="T"/>
    /// that has a parameterless constructor.
    /// </summary>
    /// <remarks>
    /// A module is a registration descriptor, not a service: it runs before the container exists,
    /// so it cannot have dependencies injected. Anything it needs comes from the configuration it
    /// is handed.
    /// </remarks>
    private static List<T> Instantiate<T>(Assembly[] assemblies, ILogger logger)
        where T : class
    {
        var instances = new List<T>();

        foreach (var assembly in assemblies)
        {
            IEnumerable<Type> types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // A half-loadable assembly must not take the host down; use what did load.
                logger.LogWarning(ex, "Could not read every type from {Assembly}.", assembly.GetName().Name);
                types = ex.Types.OfType<Type>();
            }

            foreach (var type in types)
            {
                if (!typeof(T).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) is null)
                {
                    logger.LogWarning(
                        "{Type} implements {Contract} but has no parameterless constructor; skipped.",
                        type.FullName,
                        typeof(T).Name);
                    continue;
                }

                if (Activator.CreateInstance(type) is T instance)
                {
                    instances.Add(instance);
                }
            }
        }

        return instances;
    }

    /// <summary>Compares the major and minor parts only; patch releases stay compatible.</summary>
    private static bool IsCompatible(string required, string actual)
    {
        static (int Major, int Minor) Parse(string value)
        {
            var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
            var minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
            return (major, minor);
        }

        return Parse(required) == Parse(actual);
    }
}

