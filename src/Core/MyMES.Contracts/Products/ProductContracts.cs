using System.ComponentModel.DataAnnotations;

namespace MyMES.Contracts.Products;

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

/// <summary>One revision of a product.</summary>
public sealed record ProductVersionDto(
    Guid Id,
    Guid ProductId,
    string VersionNumber,
    ProductVersionStatusDto Status,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? ReleasedAtUtc,
    DateTime? ObsoletedAtUtc);

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
public sealed record AddProductVersionRequest
{
    [Required]
    [StringLength(32, MinimumLength = 1)]
    public string VersionNumber { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Notes { get; set; }
}

