using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MyMES.EventBus;

public static class EventBusServiceCollectionExtensions
{
    /// <summary>Registers the in-process event bus. Safe to call more than once.</summary>
    public static IServiceCollection AddInProcessEventBus(this IServiceCollection services)
    {
        services.TryAddScoped<IEventBus, InProcessEventBus>();
        return services;
    }

    /// <summary>
    /// Registers a handler for one event type. Several handlers may be registered for the same
    /// event; they all run, which is how modules react to each other without being coupled.
    /// </summary>
    public static IServiceCollection AddEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : class
        where THandler : class, IEventHandler<TEvent>
    {
        services.AddScoped<IEventHandler<TEvent>, THandler>();
        return services;
    }
}
