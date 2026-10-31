using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMES.Abstractions.Results;
using MyMES.Abstractions.Time;
using MyMES.Application.Abstractions;
using MyMES.Application.Mapping;
using MyMES.Contracts.Common;
using MyMES.Contracts.Products;
using MyMES.Domain.Common;
using MyMES.Domain.Products;

namespace MyMES.Application.Products;

/// <inheritdoc cref="IProductService"/>
public sealed class ProductService(
    IApplicationDbContext db,
    IDateTimeProvider clock,
    ILogger<ProductService> logger) : IProductService
{
    public async Task<PagedResult<ProductSummaryDto>> GetAsync(
        PageRequest page,
        bool? activeOnly = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var paging = page.Normalised();
        var query = db.Products.AsNoTracking().Include(p => p.Versions).AsQueryable();

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
        var product = await db.Products
            .AsNoTracking()
            .Include(p => p.Versions)
            .SingleOrDefaultAsync(p => p.ProductCode == code, cancellationToken)
            .ConfigureAwait(false);

        return product is null
            ? Result.Failure<ProductDto>(Error.NotFound("product.not_found", $"No product with code {code}."))
            : Result.Success(product.ToDto());
    }

    public async Task<Result<ProductDto>> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = request.ProductCode.Trim().ToUpperInvariant();

        if (await db.Products.AnyAsync(p => p.ProductCode == code, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProductDto>(Error.Conflict(
                "product.code_in_use",
                $"Product code {code} is already in use."));
        }

        try
        {
            var product = Product.Create(
                request.ProductCode,
                request.Name,
                request.Description,
                request.UnitOfMeasure);

            // Creating the first revision here saves a second call for the common case.
            if (!string.IsNullOrWhiteSpace(request.InitialVersionNumber))
            {
                product.AddVersion(request.InitialVersionNumber, notes: null, clock.UtcNow);
            }

            db.Products.Add(product);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Product {ProductCode} created with id {ProductId}.", product.ProductCode, product.Id);
            return Result.Success(product.ToDto());
        }
        catch (DomainException ex)
        {
            return Result.Failure<ProductDto>(Error.Validation(ex.Code, ex.Message));
        }
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

        try
        {
            product.UpdateDetails(request.Name, request.Description, request.UnitOfMeasure);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(product.ToDto());
        }
        catch (DomainException ex)
        {
            return Result.Failure<ProductDto>(Error.Validation(ex.Code, ex.Message));
        }
    }

    public async Task<Result<ProductVersionDto>> AddVersionAsync(
        Guid productId,
        AddProductVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await LoadAsync(productId, tracking: true, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return Result.Failure<ProductVersionDto>(NotFound(productId));
        }

        try
        {
            var version = product.AddVersion(request.VersionNumber, request.Notes, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Revision {VersionNumber} added to product {ProductCode}.",
                version.VersionNumber,
                product.ProductCode);

            return Result.Success(version.ToDto());
        }
        catch (DomainException ex)
        {
            // A duplicate revision number is a conflict; everything else is bad input.
            var error = ex.Code == "product_version.duplicate"
                ? Error.Conflict(ex.Code, ex.Message)
                : Error.Validation(ex.Code, ex.Message);
            return Result.Failure<ProductVersionDto>(error);
        }
    }

    public Task<Result<ProductDto>> ReleaseVersionAsync(
        Guid productId,
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        MutateAsync(productId, (product, now) => product.ReleaseVersion(versionId, now), cancellationToken);

    public Task<Result<ProductDto>> ObsoleteVersionAsync(
        Guid productId,
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        MutateAsync(productId, (product, now) => product.ObsoleteVersion(versionId, now), cancellationToken);

    public Task<Result<ProductDto>> DeactivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, (product, now) => product.Deactivate(now), cancellationToken);

    private async Task<Result<ProductDto>> MutateAsync(
        Guid productId,
        Action<Product, DateTime> change,
        CancellationToken cancellationToken)
    {
        var product = await LoadAsync(productId, tracking: true, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return Result.Failure<ProductDto>(NotFound(productId));
        }

        try
        {
            change(product, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(product.ToDto());
        }
        catch (DomainException ex)
        {
            var error = ex.Code.EndsWith("not_found", StringComparison.Ordinal)
                ? Error.NotFound(ex.Code, ex.Message)
                : Error.Conflict(ex.Code, ex.Message);
            return Result.Failure<ProductDto>(error);
        }
    }

    /// <summary>
    /// Loads a product with its revisions. The whole aggregate is loaded because its invariants
    /// span the revisions: releasing one has to see the others.
    /// </summary>
    private Task<Product?> LoadAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.Products : db.Products.AsNoTracking();
        return query.Include(p => p.Versions).SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    private static Error NotFound(Guid id) =>
        Error.NotFound("product.not_found", $"No product with id {id}.");
}
