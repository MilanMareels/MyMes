namespace MyMES.Domain.Common;

/// <summary>
/// Something that happened in the domain and that other parts of the system may react to.
/// Events are raised by aggregates and dispatched after the change is committed.
/// </summary>
public interface IDomainEvent
{
    /// <summary>When the event occurred, in UTC.</summary>
    DateTime OccurredAtUtc { get; }
}

/// <summary>
/// Base class for entities with identity.
/// </summary>
/// <remarks>
/// Identifiers are UUID version 7: globally unique like any GUID, but time-ordered, so rows
/// insert at the end of the clustered index instead of fragmenting it the way random GUIDs do.
/// </remarks>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected Entity(Guid id) => Id = id;

    /// <summary>Required by EF Core materialisation; not intended for application code.</summary>
    protected Entity()
    {
    }

    public Guid Id { get; protected init; }

    /// <summary>Events raised by this aggregate and not yet dispatched.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Called by the persistence layer once the events have been handed to the bus.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>Creates a new time-ordered identifier for an aggregate.</summary>
    protected static Guid NewId() => Guid.CreateVersion7();
}

/// <summary>
/// An entity that records when it was created and last changed.
/// </summary>
/// <remarks>
/// Applied only where the history genuinely matters: master data and orders. Pure lookup tables
/// and owned value objects do not inherit from it, because empty audit columns are noise.
/// </remarks>
public abstract class AuditableEntity : Entity
{
    protected AuditableEntity(Guid id) : base(id)
    {
    }

    protected AuditableEntity()
    {
    }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }
}
