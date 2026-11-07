using System.ComponentModel.DataAnnotations;

namespace MyMES.Contracts.Production;

/// <summary>Production order status as published on the wire.</summary>
public enum ProductionOrderStatusDto
{
    Draft = 0,
    Released = 1,
    InProgress = 2,
    OnHold = 3,
    Completed = 4,
    Cancelled = 5
}

/// <summary>A production order as returned by the API.</summary>
public sealed record ProductionOrderDto(
    Guid Id,
    string OrderNumber,
    string ProductCode,
    Guid? ProductVersionId,
    string? ProductVersionNumber,
    decimal PlannedQuantity,
    decimal ProducedQuantity,
    decimal CompletionPercentage,
    ProductionOrderStatusDto Status,
    DateTime PlannedStartUtc,
    DateTime PlannedEndUtc,
    DateTime? ActualStartUtc,
    DateTime? ActualEndUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Creates a production order in draft.</summary>
public sealed record CreateProductionOrderRequest
{
    [Required]
    [StringLength(32, MinimumLength = 1)]
    public string OrderNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>Optional revision to build against. Required for full as-built traceability.</summary>
    public Guid? ProductVersionId { get; set; }

    [Range(0.0001, 1_000_000_000)]
    public decimal PlannedQuantity { get; set; }

    [Required]
    public DateTime PlannedStartUtc { get; set; }

    [Required]
    public DateTime PlannedEndUtc { get; set; }
}

/// <summary>Books produced quantity against a running order.</summary>
public sealed record ReportProductionRequest
{
    [Range(0.0001, 1_000_000_000)]
    public decimal Quantity { get; set; }
}

/// <summary>Reschedules an order that has not started yet.</summary>
public sealed record RescheduleProductionOrderRequest
{
    [Required]
    public DateTime PlannedStartUtc { get; set; }

    [Required]
    public DateTime PlannedEndUtc { get; set; }
}

