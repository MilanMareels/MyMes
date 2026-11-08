using System.ComponentModel.DataAnnotations;

namespace MyMES.Application.Products;

/// <summary>Product revision status as published on the wire.</summary>
public enum ProductVersionStatusDto
{
    Draft = 0,
    Released = 1,
    Obsolete = 2
}

/// <summary>A product together with its revisions.</summary>
public sealed record ProductDto(
    Guid Id,
    string ProductCode,
    string Name,
    string? Description,
    string UnitOfMeasure,
    bool IsActive,
    Guid? ReleasedVersionId,
    string? ReleasedVersionNumber,
    IReadOnlyList<ProductVersionDto> Versions,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>A summary row for product lists, without the revision detail.</summary>
public sealed record ProductSummaryDto(
    Guid Id,
    string ProductCode,
    string Name,
    string UnitOfMeasure,
    bool IsActive,
    string? ReleasedVersionNumber,
    int VersionCount);

public sealed record ComponentProductSummaryDto(
    Guid Id,
    string ProductCode,
    string Name,
    string UnitOfMeasure);

/// <summary>One revision of a product.</summary>
public sealed record ProductVersionDto(
    Guid Id,
    Guid ProductId,
    string VersionNumber,
    ProductVersionStatusDto Status,
    string? Notes,
    string? VersionDescription,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    IReadOnlyList<BillOfMaterialItemDto> BomItems,
    IReadOnlyList<RoutingStepDto> RoutingSteps,
    DateTime CreatedAtUtc,
    DateTime? ReleasedAtUtc,
    DateTime? ObsoletedAtUtc);

public sealed record BillOfMaterialItemDto(
    Guid Id,
    Guid ProductVersionId,
    Guid ComponentProductId,
    ComponentProductSummaryDto ComponentProduct,
    decimal Quantity,
    Guid? RoutingStepId);

public sealed record RoutingStepDto(
    Guid Id,
    Guid ProductVersionId,
    int Sequence,
    string Name,
    string? Description);

/// <summary>Creates a product. An initial draft revision can be created in the same call.</summary>
public sealed record CreateProductRequest
{
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string ProductCode { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(16)]
    public string UnitOfMeasure { get; set; } = "pcs";

    /// <summary>When set, a draft revision with this number is created alongside the product.</summary>
    [StringLength(32)]
    public string? InitialVersionNumber { get; set; }
}

/// <summary>Updates the descriptive fields of a product. The code is immutable.</summary>
public sealed record UpdateProductRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(16)]
    public string UnitOfMeasure { get; set; } = "pcs";
}

/// <summary>Adds a draft revision to an existing product.</summary>
public sealed record CreateProductVersionRequest
{
    [Required]
    [StringLength(32, MinimumLength = 1)]
    public string VersionNumber { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Notes { get; set; }

    [StringLength(500)]
    public string? VersionDescription { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    /// <summary>The Bill of Materials items for this version.</summary>
    public List<CreateBillOfMaterialItemRequest> BomItems { get; set; } = new();

    /// <summary>The manufacturing routing steps for this version.</summary>
    public List<CreateRoutingStepRequest> RoutingSteps { get; set; } = new();
}

public sealed record CreateBillOfMaterialItemRequest
{
    [Required]
    public Guid ComponentProductId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>Optional index or reference matching a routing step in the same creation payload.</summary>
    public int? RoutingStepSequence { get; set; }
}

public sealed record CreateRoutingStepRequest
{
    [Range(1, int.MaxValue)]
    public int Sequence { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }
}