using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MyMES.EventBus;

/// <summary>
/// Dispatches events to handlers resolved from the container, in the caller's scope.
/// </summary>
/// <remarks>
/// A failing handler is logged and the remaining handlers still run. One module's bug must not
/// stop another module from reacting, and the publisher has usually already committed its work.
/// </remarks>
public sealed class InProcessEventBus(IServiceProvider services, ILogger<InProcessEventBus> logger) : IEventBus
{
    /// <summary>Cached reflection per event type; building it on every publish would be wasteful.</summary>
    private static readonly ConcurrentDictionary<Type, DispatchPlan> Plans = new();

    public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(message);
        return PublishAsync((object)message, cancellationToken);
    }

    public async Task PublishAsync(object message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var plan = Plans.GetOrAdd(message.GetType(), static type => new DispatchPlan(type));
        var handlers = services.GetServices(plan.HandlerType).ToList();

        if (handlers.Count == 0)
        {
            logger.LogDebug("No handler registered for {EventType}.", plan.EventType.Name);
            return;
        }

        foreach (var handler in handlers)
        {
            if (handler is null)
            {
                continue;
            }

            try
            {
                await plan.InvokeAsync(handler, message, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Handler {Handler} failed for {EventType}; continuing with the remaining handlers.",
                    handler.GetType().Name,
                    plan.EventType.Name);
            }
        }
    }

    /// <summary>Pre-resolved handler type and invoker for one event type.</summary>
    private sealed class DispatchPlan
    {
        private readonly MethodInfo _handleMethod;

        public DispatchPlan(Type eventType)
        {
            EventType = eventType;
            HandlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
            _handleMethod = HandlerType.GetMethod(nameof(IEventHandler<object>.HandleAsync))
                ?? throw new InvalidOperationException(
                    $"{nameof(IEventHandler<object>.HandleAsync)} not found on {HandlerType}.");
        }

        public Type EventType { get; }

        public Type HandlerType { get; }

        public Task InvokeAsync(object handler, object message, CancellationToken cancellationToken) =>
            (Task)_handleMethod.Invoke(handler, [message, cancellationToken])!;
    }
}
