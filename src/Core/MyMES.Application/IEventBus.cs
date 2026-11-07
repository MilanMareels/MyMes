using System.Diagnostics.CodeAnalysis;

namespace MyMES.EventBus;

/// <summary>
/// Handles one kind of event. Register as many handlers per event as needed; they all run.
/// </summary>
/// <typeparam name="TEvent">Event type this handler reacts to.</typeparam>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Handler is the established vocabulary for this role in .NET messaging; the type is an interface, not a delegate.")]
public interface IEventHandler<in TEvent>
    where TEvent : class
{
    /// <summary>Reacts to a published event.</summary>
    /// <param name="message">The event that was published.</param>
    /// <param name="cancellationToken">Cancels the handler's own work.</param>
    Task HandleAsync(TEvent message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Publishes events to whichever handlers are registered for them.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation is in-process and synchronous with the publisher's lifetime, which
/// is the right shape for a modular monolith: modules stay decoupled without the operational cost
/// of a broker. Swapping in a durable transport later means replacing this registration only.
/// </para>
/// <para>
/// Publishing is <em>not</em> transactional. A handler that must not be lost needs an outbox;
/// see <c>docs/architecture/overview.md</c>.
/// </para>
/// </remarks>
public interface IEventBus
{
    /// <summary>Publishes an event whose type is known at compile time.</summary>
    Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <summary>
    /// Publishes an event whose type is only known at run time, which is how domain events
    /// collected from a change tracker are dispatched.
    /// </summary>
    Task PublishAsync(object message, CancellationToken cancellationToken = default);
}
