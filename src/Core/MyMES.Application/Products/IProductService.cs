using MyMES.Abstractions.Results;
using MyMES.Contracts.Common;
using MyMES.Contracts.Products;

namespace MyMES.Application.Products;

/// <summary>Use cases for products and their revisions.</summary>
public interface IProductService
{
    Task<PagedResult<ProductSummaryDto>> GetAsync(
        PageRequest page,
        bool? activeOnly = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<Result<ProductDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ProductDto>> GetByCodeAsync(string productCode, CancellationToken cancellationToken = default);

    Task<Result<ProductDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    Task<Result<ProductDto>> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ProductVersionDto>> AddVersionAsync(
        Guid productId,
        AddProductVersionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Promotes a draft revision to released, retiring whichever revision was current.</summary>
    Task<Result<ProductDto>> ReleaseVersionAsync(
        Guid productId,
        Guid versionId,
        CancellationToken cancellationToken = default);

    Task<Result<ProductDto>> ObsoleteVersionAsync(
        Guid productId,
        Guid versionId,
        CancellationToken cancellationToken = default);

    Task<Result<ProductDto>> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
