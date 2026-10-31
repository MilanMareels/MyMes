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
/// An instruction to produce a quantity of a product by a planned date.
/// </summary>
/// <remarks>
/// The status transitions are enforced here rather than in a service, so there is exactly one
/// place that decides whether an order may start, pause, resume or complete.
/// </remarks>
public sealed class ProductionOrder : AuditableEntity
{
    private ProductionOrder()
    {
    }

    private ProductionOrder(
        Guid id,
        string orderNumber,
        string productCode,
        Guid? productVersionId,
        decimal plannedQuantity,
        DateTime plannedStartUtc,
        DateTime plannedEndUtc) : base(id)
    {
        OrderNumber = orderNumber;
        ProductCode = productCode;
        ProductVersionId = productVersionId;
        PlannedQuantity = plannedQuantity;
        PlannedStartUtc = plannedStartUtc;
        PlannedEndUtc = plannedEndUtc;
        Status = ProductionOrderStatus.Draft;
    }

    /// <summary>Unique business key, for example <c>WO-2026-0001</c>.</summary>
    public string OrderNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Code of the product being made, captured on the order so it survives master-data changes.
    /// </summary>
    public string ProductCode { get; private set; } = string.Empty;

    /// <summary>
    /// The exact revision this order is built against. Optional, because an order may be raised
    /// before a revision is released, but required for full as-built traceability.
    /// </summary>
    public Guid? ProductVersionId { get; private set; }

    public decimal PlannedQuantity { get; private set; }

    public decimal ProducedQuantity { get; private set; }

    public ProductionOrderStatus Status { get; private set; }

    public DateTime PlannedStartUtc { get; private set; }

    public DateTime PlannedEndUtc { get; private set; }

    public DateTime? ActualStartUtc { get; private set; }

    public DateTime? ActualEndUtc { get; private set; }

    /// <summary>How much of the planned quantity is done, capped at 100 percent.</summary>
    public decimal CompletionPercentage => PlannedQuantity <= 0
        ? 0
        : Math.Min(100m, Math.Round(ProducedQuantity / PlannedQuantity * 100m, 2));

    /// <summary>The order is finished or abandoned and accepts no further changes.</summary>
    public bool IsClosed => Status is ProductionOrderStatus.Completed or ProductionOrderStatus.Cancelled;

    /// <summary>Creates a draft order.</summary>
    /// <exception cref="DomainException">Input violates an invariant.</exception>
    public static ProductionOrder Create(
        string orderNumber,
        string productCode,
        decimal plannedQuantity,
        DateTime plannedStartUtc,
        DateTime plannedEndUtc,
        Guid? productVersionId = null)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new DomainException("production_order.number_required", "An order needs a number.");
        }

        if (string.IsNullOrWhiteSpace(productCode))
        {
            throw new DomainException("production_order.product_required", "An order needs a product code.");
        }

        if (plannedQuantity <= 0)
        {
            throw new DomainException(
                "production_order.quantity_invalid",
                "Planned quantity must be greater than zero.");
        }

        if (plannedEndUtc < plannedStartUtc)
        {
            throw new DomainException(
                "production_order.dates_invalid",
                "Planned end cannot be before planned start.");
        }

        return new ProductionOrder(
            NewId(),
            orderNumber.Trim().ToUpperInvariant(),
            productCode.Trim().ToUpperInvariant(),
            productVersionId,
            plannedQuantity,
            plannedStartUtc,
            plannedEndUtc);
    }

    /// <summary>Hands the order to the shop floor.</summary>
    public void Release(DateTime utcNow)
    {
        if (Status != ProductionOrderStatus.Draft)
        {
            throw new DomainException(
                "production_order.not_draft",
                $"Order {OrderNumber} is {Status} and cannot be released again.");
        }

        Status = ProductionOrderStatus.Released;
        UpdatedAtUtc = utcNow;
        Raise(new ProductionOrderReleasedEvent(Id, OrderNumber, ProductCode, utcNow));
    }

    /// <summary>Starts, or resumes after a hold.</summary>
    public void Start(DateTime utcNow)
    {
        if (Status is not (ProductionOrderStatus.Released or ProductionOrderStatus.OnHold))
        {
            throw new DomainException(
                "production_order.not_startable",
                $"Order {OrderNumber} is {Status} and cannot be started.");
        }

        Status = ProductionOrderStatus.InProgress;
        ActualStartUtc ??= utcNow;
        UpdatedAtUtc = utcNow;
        Raise(new ProductionOrderStartedEvent(Id, OrderNumber, utcNow));
    }

    /// <summary>Pauses a running order.</summary>
    public void PutOnHold(DateTime utcNow)
    {
        if (Status != ProductionOrderStatus.InProgress)
        {
            throw new DomainException(
                "production_order.not_running",
                $"Order {OrderNumber} is {Status} and cannot be put on hold.");
        }

        Status = ProductionOrderStatus.OnHold;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Books produced quantity against the order.
    /// </summary>
    /// <remarks>
    /// Over-production is accepted rather than rejected: the floor really does make a few extra
    /// pieces, and refusing the booking would push operators into not reporting at all. The
    /// surplus stays visible because <see cref="CompletionPercentage"/> caps at 100 while
    /// <see cref="ProducedQuantity"/> does not.
    /// </remarks>
    /// <exception cref="DomainException">The quantity is not positive, or the order is not running.</exception>
    public void ReportProduction(decimal quantity, DateTime utcNow)
    {
        if (quantity <= 0)
        {
            throw new DomainException(
                "production_order.quantity_invalid",
                "Reported quantity must be greater than zero.");
        }

        if (Status != ProductionOrderStatus.InProgress)
        {
            throw new DomainException(
                "production_order.not_running",
                $"Order {OrderNumber} is {Status}; production can only be booked while it runs.");
        }

        ProducedQuantity += quantity;
        UpdatedAtUtc = utcNow;
        Raise(new ProductionReportedEvent(Id, OrderNumber, quantity, ProducedQuantity, utcNow));
    }

    /// <summary>Closes the order as finished.</summary>
    public void Complete(DateTime utcNow)
    {
        if (Status is not (ProductionOrderStatus.InProgress or ProductionOrderStatus.OnHold))
        {
            throw new DomainException(
                "production_order.not_completable",
                $"Order {OrderNumber} is {Status} and cannot be completed.");
        }

        Status = ProductionOrderStatus.Completed;
        ActualEndUtc = utcNow;
        UpdatedAtUtc = utcNow;
        Raise(new ProductionOrderCompletedEvent(Id, OrderNumber, ProducedQuantity, utcNow));
    }

    /// <summary>Abandons the order.</summary>
    public void Cancel(DateTime utcNow)
    {
        if (IsClosed)
        {
            throw new DomainException(
                "production_order.closed",
                $"Order {OrderNumber} is already {Status}.");
        }

        Status = ProductionOrderStatus.Cancelled;
        ActualEndUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Ties the order to a specific product revision. Only possible before release.</summary>
    public void AssignProductVersion(Guid productVersionId, DateTime utcNow)
    {
        if (Status != ProductionOrderStatus.Draft)
        {
            throw new DomainException(
                "production_order.not_draft",
                $"Order {OrderNumber} is {Status}; the revision is fixed once it leaves draft.");
        }

        ProductVersionId = productVersionId;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Adjusts the plan. Only possible while the order has not started.</summary>
    public void Reschedule(DateTime plannedStartUtc, DateTime plannedEndUtc, DateTime utcNow)
    {
        if (Status is not (ProductionOrderStatus.Draft or ProductionOrderStatus.Released))
        {
            throw new DomainException(
                "production_order.started",
                $"Order {OrderNumber} is {Status} and can no longer be rescheduled.");
        }

        if (plannedEndUtc < plannedStartUtc)
        {
            throw new DomainException(
                "production_order.dates_invalid",
                "Planned end cannot be before planned start.");
        }

        PlannedStartUtc = plannedStartUtc;
        PlannedEndUtc = plannedEndUtc;
        UpdatedAtUtc = utcNow;
    }
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
