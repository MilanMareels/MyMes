using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMES.Abstractions.Results;
using MyMES.Abstractions.Time;
using MyMES.Application.Data;
using MyMES.Application.Mapping;
using MyMES.Contracts.Common;
using MyMES.Contracts.Products;
using MyMES.Domain.Products;
using MyMES.EventBus;

namespace MyMES.Application.Products;

/// <inheritdoc cref="IProductService"/>
public sealed class ProductService(IRepository<Product> products, IDateTimeProvider clock, IEventBus eventBus, ILogger<ProductService> logger) : IProductService
{
    public async Task<PagedResult<ProductSummaryDto>> GetAsync(PageRequest page, bool? activeOnly = null, string? search = null, CancellationToken cancellationToken = default)
    {
        var paging = page.Normalised();
        var query = products.Query().AsNoTracking().Include(p => p.Versions).AsQueryable();

        if (activeOnly == true)
        {
            query = query.Where(p => p.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.ProductCode.Contains(term) || p.Name.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderBy(p => p.ProductCode)
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ProductSummaryDto>(
            [.. items.Select(p => p.ToSummaryDto())],
            paging.Page,
            paging.PageSize,
            total);
    }

    public async Task<Result<ProductDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await LoadAsync(id, tracking: false, cancellationToken).ConfigureAwait(false);
        return product is null
            ? Result.Failure<ProductDto>(NotFound(id))
            : Result.Success(product.ToDto());
    }

    public async Task<Result<ProductDto>> GetByCodeAsync(
        string productCode,
        CancellationToken cancellationToken = default)
    {
        var code = productCode.Trim().ToUpperInvariant();
        var product = await products.Query()
            .AsNoTracking()
            .Include(p => p.Versions)
            .SingleOrDefaultAsync(p => p.ProductCode == code, cancellationToken)
            .ConfigureAwait(false);

        return product is null
            ? Result.Failure<ProductDto>(Error.NotFound("product.not_found", $"No product with code {code}."))
            : Result.Success(product.ToDto());
    }

    public async Task<Result<ProductDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.ProductCode.Trim().ToUpperInvariant();
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure<ProductDto>(Error.Validation("product.code_required", "A product needs a code."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<ProductDto>(Error.Validation("product.name_required", "A product needs a name."));
        }

        if (await products.Query().AnyAsync(p => p.ProductCode == code, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProductDto>(Error.Conflict(
                "product.code_in_use",
                $"Product code {code} is already in use."));
        }

        var product = new Product(Guid.NewGuid())
        {
            ProductCode = code,
            Name = name,
            Description = request.Description?.Trim(),
            UnitOfMeasure = string.IsNullOrWhiteSpace(request.UnitOfMeasure) ? "pcs" : request.UnitOfMeasure.Trim(),
            IsActive = true
        };

        // Creating the first revision here saves a second call for the common case.
        if (!string.IsNullOrWhiteSpace(request.InitialVersionNumber))
        {
            AddDraftVersion(product, request.InitialVersionNumber, notes: null, versionDescription: null);
        }

        products.Add(product);
        await products.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Product {ProductCode} created with id {ProductId}.", product.ProductCode, product.Id);
        return Result.Success(product.ToDto());
    }

    public async Task<Result<ProductDto>> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await LoadAsync(id, tracking: true, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return Result.Failure<ProductDto>(NotFound(id));
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<ProductDto>(Error.Validation("product.name_required", "A product needs a name."));
        }

        product.Name = name;
        product.Description = request.Description?.Trim();
        if (!string.IsNullOrWhiteSpace(request.UnitOfMeasure))
        {
            product.UnitOfMeasure = request.UnitOfMeasure.Trim();
        }

        await products.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(product.ToDto());
    }

    public async Task<Result<ProductVersionDto>> AddVersionAsync(Guid productId, CreateProductVersionRequest request, CancellationToken cancellationToken = default)
    {
        var product = await LoadAsync(productId, tracking: true, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return Result.Failure<ProductVersionDto>(NotFound(productId));
        }

        if (!product.IsActive)
        {
            return Result.Failure<ProductVersionDto>(Error.Validation(
                "product.inactive",
                $"Product {product.ProductCode} is inactive and cannot take new revisions."));
        }

        var versionNumber = request.VersionNumber.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(versionNumber))
        {
            return Result.Failure<ProductVersionDto>(Error.Validation(
                "product_version.number_required",
                "A revision needs a version number."));
        }

        if (product.Versions.Any(v => v.VersionNumber == versionNumber))
        {
            return Result.Failure<ProductVersionDto>(Error.Conflict(
                "product_version.duplicate",
                $"Product {product.ProductCode} already has revision {versionNumber}."));
        }

        var version = AddDraftVersion(product, versionNumber, request.Notes, request.VersionDescription);
        await products.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Revision {VersionNumber} added to product {ProductCode}.",
            version.VersionNumber,
            product.ProductCode);

        return Result.Success(version.ToDto());
    }

    public async Task<Result<ProductDto>> ReleaseVersionAsync(Guid productId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var product = await LoadAsync(productId, tracking: true, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return Result.Failure<ProductDto>(NotFound(productId));
        }

        var version = product.Versions.SingleOrDefault(v => v.Id == versionId);
        if (version is null)
        {
            return Result.Failure<ProductDto>(Error.NotFound(
                "product_version.not_found",
                $"Product {product.ProductCode} has no revision {versionId}."));
        }

        if (!product.IsActive)
        {
            return Result.Failure<ProductDto>(Error.Validation(
                "product.inactive",
                $"Product {product.ProductCode} is inactive and cannot release revisions."));
        }

        if (version.Status != ProductVersionStatus.Draft)
        {
            return Result.Failure<ProductDto>(Error.Conflict(
                "product_version.not_draft",
                $"Revision {version.VersionNumber} is {version.Status} and can no longer be released."));
        }

        // The invariant: releasing one revision retires the current one.
        var previous = product.ReleasedVersion;
        if (previous is not null)
        {
            previous.Status = ProductVersionStatus.Obsolete;
            previous.ObsoletedAtUtc = clock.UtcNow;
        }

        version.Status = ProductVersionStatus.Released;
        version.ReleasedAtUtc = clock.UtcNow;
        version.ObsoletedAtUtc = null;

        await products.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await eventBus.PublishAsync(new ProductVersionReleasedEvent(
            product.Id,
            product.ProductCode,
            version.Id,
            version.VersionNumber,
            previous?.VersionNumber,
            clock.UtcNow), cancellationToken).ConfigureAwait(false);

        return Result.Success(product.ToDto());
    }

    public async Task<Result<ProductDto>> ObsoleteVersionAsync(Guid productId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var product = await LoadAsync(productId, tracking: true, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return Result.Failure<ProductDto>(NotFound(productId));
        }

        var version = product.Versions.SingleOrDefault(v => v.Id == versionId);
        if (version is null)
        {
            return Result.Failure<ProductDto>(Error.NotFound(
                "product_version.not_found",
                $"Product {product.ProductCode} has no revision {versionId}."));
        }

        version.Status = ProductVersionStatus.Obsolete;
        version.ObsoletedAtUtc = clock.UtcNow;

        await products.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(product.ToDto());
    }

    public async Task<Result<ProductDto>> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await LoadAsync(id, tracking: true, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return Result.Failure<ProductDto>(NotFound(id));
        }

        if (product.IsActive)
        {
            product.IsActive = false;
            var released = product.ReleasedVersion;
            if (released is not null)
            {
                released.Status = ProductVersionStatus.Obsolete;
                released.ObsoletedAtUtc = clock.UtcNow;
            }

            await products.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(product.ToDto());
    }

    public async Task<Result<ProductDto>> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await LoadAsync(id, tracking: true, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return Result.Failure<ProductDto>(NotFound(id));
        }

        if (!product.IsActive)
        {
            product.IsActive = true;

            var versionToRestore = product.Versions
                .Where(v => v.ReleasedAtUtc.HasValue)
                .OrderByDescending(v => v.ObsoletedAtUtc ?? v.ReleasedAtUtc)
                .FirstOrDefault();

            if (versionToRestore is not null)
            {
                versionToRestore.Status = ProductVersionStatus.Released;
                versionToRestore.ReleasedAtUtc = clock.UtcNow;
                versionToRestore.ObsoletedAtUtc = null;
            }

            await products.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(product.ToDto());
    }

    private ProductVersion AddDraftVersion(Product product, string versionNumber, string? notes, string? versionDescription)
    {
        var version = new ProductVersion(Guid.NewGuid())
        {
            ProductId = product.Id,
            VersionNumber = versionNumber.Trim().ToUpperInvariant(),
            Notes = notes?.Trim(),
            VersionDescription = versionDescription?.Trim(),
            Status = ProductVersionStatus.Draft,
            CreatedAtUtc = clock.UtcNow
        };
        product.Versions.Add(version);
        return version;
    }

    /// <summary>Loads a product with its revisions.</summary>
    private Task<Product?> LoadAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? products.Query() : products.Query().AsNoTracking();
        return query.Include(p => p.Versions).SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    private static Error NotFound(Guid id) =>
        Error.NotFound("product.not_found", $"No product with id {id}.");
}
