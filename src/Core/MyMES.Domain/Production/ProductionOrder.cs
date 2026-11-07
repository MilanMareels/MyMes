using MyMES.Domain.Common;

namespace MyMES.Domain.Production;

/// <summary>Lifecycle of a production order.</summary>
public enum ProductionOrderStatus
{
    /// <summary>Being prepared; not visible to the floor.</summary>
    Draft = 0,

    /// <summary>Approved and handed to the floor, not yet started.</summary>
    Released = 1,

    /// <summary>Production has begun.</summary>
    InProgress = 2,

    /// <summary>Temporarily halted; can resume.</summary>
    OnHold = 3,

    /// <summary>Finished. Terminal.</summary>
    Completed = 4,

    /// <summary>Abandoned before completion. Terminal.</summary>
    Cancelled = 5
}

/// <summary>
/// An instruction to produce a quantity of a product by a planned date. A plain data object:
/// the status transitions live in the application layer.
/// </summary>
public sealed class ProductionOrder : AuditableEntity
{
    public ProductionOrder()
    {
    }

    public ProductionOrder(Guid id) : base(id)
    {
    }

    /// <summary>Unique business key, for example <c>WO-2026-0001</c>.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Code of the product being made, captured on the order so it survives master-data changes.
    /// </summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>
    /// The exact revision this order is built against. Optional, because an order may be raised
    /// before a revision is released, but required for full as-built traceability.
    /// </summary>
    public Guid? ProductVersionId { get; set; }

    public decimal PlannedQuantity { get; set; }

    public decimal ProducedQuantity { get; set; }

    public ProductionOrderStatus Status { get; set; } = ProductionOrderStatus.Draft;

    public DateTime PlannedStartUtc { get; set; }

    public DateTime PlannedEndUtc { get; set; }

    public DateTime? ActualStartUtc { get; set; }

    public DateTime? ActualEndUtc { get; set; }

    /// <summary>How much of the planned quantity is done, capped at 100 percent.</summary>
    public decimal CompletionPercentage => PlannedQuantity <= 0
        ? 0
        : Math.Min(100m, Math.Round(ProducedQuantity / PlannedQuantity * 100m, 2));

    /// <summary>The order is finished or abandoned and accepts no further changes.</summary>
    public bool IsClosed => Status is ProductionOrderStatus.Completed or ProductionOrderStatus.Cancelled;
}

public sealed record ProductionOrderReleasedEvent(
    Guid ProductionOrderId,
    string OrderNumber,
    string ProductCode,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record ProductionOrderStartedEvent(
    Guid ProductionOrderId,
    string OrderNumber,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record ProductionReportedEvent(
    Guid ProductionOrderId,
    string OrderNumber,
    decimal Quantity,
    decimal TotalProduced,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record ProductionOrderCompletedEvent(
    Guid ProductionOrderId,
    string OrderNumber,
    decimal ProducedQuantity,
    DateTime OccurredAtUtc) : IDomainEvent;
