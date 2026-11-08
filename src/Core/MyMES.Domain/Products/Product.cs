using MyMES.Domain.Common;

namespace MyMES.Domain.Products;

/// <summary>Lifecycle of a single revision of a product.</summary>
public enum ProductVersionStatus
{
    /// <summary>Being prepared; may still change and cannot be produced against.</summary>
    Draft = 0,

    /// <summary>Approved for production. At most one revision per product is released.</summary>
    Released = 1,

    /// <summary>Superseded or withdrawn. Kept for traceability of past orders.</summary>
    Obsolete = 2
}

/// <summary>
/// An item the plant manufactures, together with its revision history. A plain data object:
/// the business rules live in the application layer.
/// </summary>
public sealed class Product : AuditableEntity
{
    public Product()
    {
    }

    public Product(Guid id) : base(id)
    {
    }

    /// <summary>Unique business key, for example <c>FG-PUMP-100</c>.</summary>
    public string ProductCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Unit the product is counted in, for example <c>pcs</c> or <c>kg</c>.</summary>
    public string UnitOfMeasure { get; set; } = "pcs";

    /// <summary>Inactive products keep their history but accept no new orders or revisions.</summary>
    public bool IsActive { get; set; } = true;

    public List<ProductVersion> Versions { get; set; } = [];

    /// <summary>The revision currently approved for production, if any.</summary>
    public ProductVersion? ReleasedVersion =>
        Versions.SingleOrDefault(v => v.Status == ProductVersionStatus.Released);
}

/// <summary>One revision of a product: the thing an order is actually built against.</summary>
public sealed class ProductVersion : Entity
{
    public ProductVersion()
    {
    }

    public ProductVersion(Guid id) : base(id)
    {
    }

    public Guid ProductId { get; set; }

    /// <summary>Revision label, unique within the product, for example <c>A</c> or <c>1.2</c>.</summary>
    public string VersionNumber { get; set; } = string.Empty;

    public ProductVersionStatus Status { get; set; } = ProductVersionStatus.Draft;

    public string? Notes { get; set; }

    public string? VersionDescription { get; set; } = string.Empty;

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public string? ChangeLog { get; set; }

    public ICollection<BillOfMaterialItem> BomItems { get; set; } = new List<BillOfMaterialItem>();
    public ICollection<RoutingStep> RoutingSteps { get; set; } = new List<RoutingStep>();

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>When this revision became the released one.</summary>
    public DateTime? ReleasedAtUtc { get; set; }

    /// <summary>When this revision stopped being current.</summary>
    public DateTime? ObsoletedAtUtc { get; set; }

    /// <summary>Navigation back to the owning product; set by EF Core.</summary>
    public Product? Product { get; set; }
}

public class BillOfMaterialItem : Entity
{
    public BillOfMaterialItem()
    {
    }

    public BillOfMaterialItem(Guid id) : base(id)
    {
    }

    public Guid ProductVersionId { get; set; }
    public ProductVersion ProductVersion { get; set; } = null!;

    public Guid ComponentProductId { get; set; }
    public Product ComponentProduct { get; set; } = null!;

    public decimal Quantity { get; set; }

    public Guid? RoutingStepId { get; set; }
    public RoutingStep? RoutingStep { get; set; }
}

public class RoutingStep : Entity
{
    public RoutingStep()
    {
    }

    public RoutingStep(Guid id) : base(id)
    {
    }

    public Guid ProductVersionId { get; set; }
    public ProductVersion ProductVersion { get; set; } = null!;

    public int Sequence { get; set; }
    public string Name { get; set; } = string.Empty; // Product name or step name
    public string? Description { get; set; }

    public ICollection<BillOfMaterialItem> BomItems { get; set; } = new List<BillOfMaterialItem>();
}

/// <summary>Raised when a revision becomes the released one for its product.</summary>
public sealed record ProductVersionReleasedEvent(
    Guid ProductId,
    string ProductCode,
    Guid ProductVersionId,
    string VersionNumber,
    string? SupersededVersionNumber,
    DateTime OccurredAtUtc) : IDomainEvent;
