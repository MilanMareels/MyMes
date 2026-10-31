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
/// An item the plant manufactures, together with its revision history.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Product"/> is the aggregate root and <see cref="ProductVersion"/> lives inside it.
/// That boundary is deliberate: the rule "at most one released revision at a time" spans all
/// revisions, so only the root can enforce it consistently.
/// </para>
/// <para>
/// Production orders reference a specific <see cref="ProductVersion"/>, which is what makes a
/// finished batch traceable to the exact revision it was built against.
/// </para>
/// </remarks>
public sealed class Product : AuditableEntity
{
    private readonly List<ProductVersion> _versions = [];

    private Product()
    {
    }

    private Product(Guid id, string productCode, string name, string? description, string unitOfMeasure)
        : base(id)
    {
        ProductCode = productCode;
        Name = name;
        Description = description;
        UnitOfMeasure = unitOfMeasure;
        IsActive = true;
    }

    /// <summary>Unique business key, for example <c>FG-PUMP-100</c>.</summary>
    public string ProductCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Unit the product is counted in, for example <c>pcs</c> or <c>kg</c>.</summary>
    public string UnitOfMeasure { get; private set; } = "pcs";

    /// <summary>Inactive products keep their history but accept no new orders or revisions.</summary>
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<ProductVersion> Versions => _versions.AsReadOnly();

    /// <summary>The revision currently approved for production, if any.</summary>
    public ProductVersion? ReleasedVersion =>
        _versions.SingleOrDefault(v => v.Status == ProductVersionStatus.Released);

    public static Product Create(string productCode, string name, string? description, string unitOfMeasure)
    {
        if (string.IsNullOrWhiteSpace(productCode))
        {
            throw new DomainException("product.code_required", "A product needs a code.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("product.name_required", "A product needs a name.");
        }

        return new Product(
            NewId(),
            productCode.Trim().ToUpperInvariant(),
            name.Trim(),
            description?.Trim(),
            string.IsNullOrWhiteSpace(unitOfMeasure) ? "pcs" : unitOfMeasure.Trim());
    }

    public void UpdateDetails(string name, string? description, string unitOfMeasure)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("product.name_required", "A product needs a name.");
        }

        Name = name.Trim();
        Description = description?.Trim();
        UnitOfMeasure = string.IsNullOrWhiteSpace(unitOfMeasure) ? UnitOfMeasure : unitOfMeasure.Trim();
    }

    /// <summary>Adds a new draft revision.</summary>
    /// <exception cref="DomainException">
    /// The product is inactive, the version number is blank, or that number already exists.
    /// </exception>
    public ProductVersion AddVersion(string versionNumber, string? notes, DateTime utcNow)
    {
        if (!IsActive)
        {
            throw new DomainException(
                "product.inactive",
                $"Product {ProductCode} is inactive and cannot take new revisions.");
        }

        if (string.IsNullOrWhiteSpace(versionNumber))
        {
            throw new DomainException("product_version.number_required", "A revision needs a version number.");
        }

        var normalised = versionNumber.Trim().ToUpperInvariant();
        if (_versions.Any(v => v.VersionNumber == normalised))
        {
            throw new DomainException(
                "product_version.duplicate",
                $"Product {ProductCode} already has revision {normalised}.");
        }

        var version = ProductVersion.CreateDraft(Id, normalised, notes, utcNow);
        _versions.Add(version);
        return version;
    }

    /// <summary>
    /// Promotes a draft revision to released, superseding whichever revision was released before.
    /// </summary>
    /// <exception cref="DomainException">
    /// The product is inactive, the revision is unknown, or it is not a draft.
    /// </exception>
    public void ReleaseVersion(Guid versionId, DateTime utcNow)
    {
        if (!IsActive)
        {
            throw new DomainException(
                "product.inactive",
                $"Product {ProductCode} is inactive and cannot release revisions.");
        }

        var version = _versions.SingleOrDefault(v => v.Id == versionId)
            ?? throw new DomainException(
                "product_version.not_found",
                $"Product {ProductCode} has no revision {versionId}.");

        if (version.Status != ProductVersionStatus.Draft)
        {
            throw new DomainException(
                "product_version.not_draft",
                $"Revision {version.VersionNumber} is {version.Status} and can no longer be released.");
        }

        // The invariant lives here, on the root: releasing one revision retires the current one.
        var previous = ReleasedVersion;
        previous?.MarkObsolete(utcNow);

        version.Release(utcNow);

        Raise(new ProductVersionReleasedEvent(
            Id,
            ProductCode,
            version.Id,
            version.VersionNumber,
            previous?.VersionNumber,
            utcNow));
    }

    /// <summary>Withdraws a revision without replacing it.</summary>
    public void ObsoleteVersion(Guid versionId, DateTime utcNow)
    {
        var version = _versions.SingleOrDefault(v => v.Id == versionId)
            ?? throw new DomainException(
                "product_version.not_found",
                $"Product {ProductCode} has no revision {versionId}.");

        version.MarkObsolete(utcNow);
    }

    /// <summary>
    /// Deactivates the product. Its released revision is retired at the same time, so nothing can
    /// be ordered against a product that is no longer current.
    /// </summary>
    public void Deactivate(DateTime utcNow)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        ReleasedVersion?.MarkObsolete(utcNow);
    }

    public void Reactivate() => IsActive = true;
}

/// <summary>
/// One revision of a product: the thing an order is actually built against.
/// </summary>
public sealed class ProductVersion : Entity
{
    private ProductVersion()
    {
    }

    private ProductVersion(Guid id, Guid productId, string versionNumber, string? notes, DateTime utcNow)
        : base(id)
    {
        ProductId = productId;
        VersionNumber = versionNumber;
        Notes = notes;
        Status = ProductVersionStatus.Draft;
        CreatedAtUtc = utcNow;
    }

    public Guid ProductId { get; private set; }

    /// <summary>Revision label, unique within the product, for example <c>A</c> or <c>1.2</c>.</summary>
    public string VersionNumber { get; private set; } = string.Empty;

    public ProductVersionStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>When this revision became the released one.</summary>
    public DateTime? ReleasedAtUtc { get; private set; }

    /// <summary>When this revision stopped being current.</summary>
    public DateTime? ObsoletedAtUtc { get; private set; }

    /// <summary>Navigation back to the owning aggregate; set by EF Core.</summary>
    public Product? Product { get; private set; }

    internal static ProductVersion CreateDraft(Guid productId, string versionNumber, string? notes, DateTime utcNow) =>
        new(NewId(), productId, versionNumber, notes?.Trim(), utcNow);

    internal void Release(DateTime utcNow)
    {
        Status = ProductVersionStatus.Released;
        ReleasedAtUtc = utcNow;
        ObsoletedAtUtc = null;
    }

    internal void MarkObsolete(DateTime utcNow)
    {
        if (Status == ProductVersionStatus.Obsolete)
        {
            return;
        }

        Status = ProductVersionStatus.Obsolete;
        ObsoletedAtUtc = utcNow;
    }

    /// <summary>Updates the free-text notes. Allowed on a draft only, since released revisions are frozen.</summary>
    public void UpdateNotes(string? notes)
    {
        if (Status != ProductVersionStatus.Draft)
        {
            throw new DomainException(
                "product_version.frozen",
                $"Revision {VersionNumber} is {Status} and can no longer be edited.");
        }

        Notes = notes?.Trim();
    }
}

/// <summary>Raised when a revision becomes the released one for its product.</summary>
public sealed record ProductVersionReleasedEvent(
    Guid ProductId,
    string ProductCode,
    Guid ProductVersionId,
    string VersionNumber,
    string? SupersededVersionNumber,
    DateTime OccurredAtUtc) : IDomainEvent;
