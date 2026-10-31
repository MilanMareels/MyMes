using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MyMES.PluginSystem;

/// <summary>
/// The three ways an extension changes the platform: replace, decorate, extend.
/// </summary>
/// <remarks>
/// These are thin wrappers over what the container already offers, but naming them makes the
/// intent explicit at the call site and gives the documentation something concrete to point at.
/// </remarks>
public static class ExtensibilityExtensions
{
    /// <summary>
    /// <strong>Replace.</strong> Swaps the registered implementation of
    /// <typeparamref name="TService"/> for <typeparamref name="TImplementation"/>.
    /// </summary>
    /// <remarks>
    /// Use when the default behaviour is wrong for this installation; for example a customer's own
    /// order-numbering scheme. The default is gone afterwards.
    /// </remarks>
    public static IServiceCollection ReplaceService<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<TService>();
        services.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime));
        return services;
    }

    /// <summary>
    /// <strong>Decorate.</strong> Wraps the existing <typeparamref name="TService"/> registration
    /// so <typeparamref name="TDecorator"/> runs around it.
    /// </summary>
    /// <remarks>
    /// Use to add behaviour while keeping the original: auditing, caching, pushing a copy of every
    /// booking to an ERP. The decorator takes the previous implementation as a constructor
    /// parameter. Backed by Scrutor.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Nothing is registered for the service.</exception>
    public static IServiceCollection DecorateService<TService, TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.All(d => d.ServiceType != typeof(TService)))
        {
            throw new InvalidOperationException(
                $"Cannot decorate {typeof(TService).Name}: no implementation is registered. "
                + "Decorating runs after the modules, so check the registration order.");
        }

        services.Decorate<TService, TDecorator>();
        return services;
    }

    /// <summary>
    /// <strong>Extend.</strong> Adds an implementation alongside whatever is already registered.
    /// </summary>
    /// <remarks>
    /// Use for anything the host resolves as a collection: extra event handlers, extra machine
    /// drivers, extra quality checks. Nothing existing is affected.
    /// </remarks>
    public static IServiceCollection ExtendWith<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime));
        return services;
    }
}
