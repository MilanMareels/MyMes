using MyMES.Abstractions.Results;
using MyMES.Contracts.Common;
using MyMES.Contracts.Production;

namespace MyMES.Application.Production;

/// <summary>Use cases for production orders and their lifecycle.</summary>
public interface IProductionOrderService
{
    Task<PagedResult<ProductionOrderDto>> GetAsync(
        PageRequest page,
        ProductionOrderStatusDto? status = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<Result<ProductionOrderDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ProductionOrderDto>> CreateAsync(
        CreateProductionOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ProductionOrderDto>> ReleaseAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ProductionOrderDto>> StartAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ProductionOrderDto>> PutOnHoldAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ProductionOrderDto>> ReportProductionAsync(
        Guid id,
        ReportProductionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ProductionOrderDto>> CompleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ProductionOrderDto>> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}
