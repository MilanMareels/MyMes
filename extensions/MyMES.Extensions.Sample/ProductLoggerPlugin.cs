using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMES.Abstractions.Results;
using MyMES.Application.Products;
using MyMES.Contracts.Common;
using MyMES.Contracts.Products;
using MyMES.PluginSystem;

namespace MyMES.Extensions.Sample;

public sealed class ProductLoggerPlugin : IMyMesPlugin
{
    public string Name => "Product logger";

    public string Version => "1.0.0";

    public string CompatiblePlatformVersion => "0.1";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Extensions:ProductLogger:Enabled"))
        {
            return;
        }

        services.DecorateService<IProductService, ProductLoggingDecorator>();
    }
}

internal sealed class ProductLoggingDecorator(IProductService inner,ILogger<ProductLoggingDecorator> logger) : IProductService
{
    public async Task<Result<ProductDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var result = await inner.CreateAsync(request, cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "[product-logger] Product {Code} ('{Name}') aangemaakt.",
                result.Value.ProductCode,
                result.Value.Name);
        }

        return result;
    }

    // ---------- Alles hieronder geven we alleen maar door ----------
    public Task<PagedResult<ProductSummaryDto>> GetAsync(
        PageRequest page,
        bool? activeOnly = null,
        string? search = null,
        CancellationToken cancellationToken = default) =>
        inner.GetAsync(page, activeOnly, search, cancellationToken);

    public Task<Result<ProductDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.GetByIdAsync(id, cancellationToken);

    public Task<Result<ProductDto>> GetByCodeAsync(string productCode, CancellationToken cancellationToken = default) =>
        inner.GetByCodeAsync(productCode, cancellationToken);

    public Task<Result<ProductDto>> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default) =>
        inner.UpdateAsync(id, request, cancellationToken);

    public Task<Result<ProductVersionDto>> AddVersionAsync(
        Guid productId,
        AddProductVersionRequest request,
        CancellationToken cancellationToken = default) =>
        inner.AddVersionAsync(productId, request, cancellationToken);

    public Task<Result<ProductDto>> ReleaseVersionAsync(
        Guid productId,
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        inner.ReleaseVersionAsync(productId, versionId, cancellationToken);

    public Task<Result<ProductDto>> ObsoleteVersionAsync(
        Guid productId,
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        inner.ObsoleteVersionAsync(productId, versionId, cancellationToken);

    public Task<Result<ProductDto>> DeactivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.DeactivateAsync(id, cancellationToken);
}
