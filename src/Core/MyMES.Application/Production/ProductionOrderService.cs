using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMES.Abstractions.Results;
using MyMES.Abstractions.Time;
using MyMES.Application.Abstractions;
using MyMES.Application.Mapping;
using MyMES.Contracts.Common;
using MyMES.Contracts.Production;
using MyMES.Domain.Common;
using MyMES.Domain.Production;
using MyMES.Domain.Products;

namespace MyMES.Application.Production;

/// <inheritdoc cref="IProductionOrderService"/>
public sealed class ProductionOrderService(
    IApplicationDbContext db,
    IDateTimeProvider clock,
    ILogger<ProductionOrderService> logger) : IProductionOrderService
{
    public async Task<PagedResult<ProductionOrderDto>> GetAsync(
        PageRequest page,
        ProductionOrderStatusDto? status = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var paging = page.Normalised();
        var query = db.ProductionOrders.AsNoTracking();

        if (status is { } wanted)
        {
            var domainStatus = wanted.ToDomain();
            query = query.Where(o => o.Status == domainStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o => o.OrderNumber.Contains(term) || o.ProductCode.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Projected in one query so the revision label does not cost a round trip per row.
        var rows = await query
            .OrderBy(o => o.Status == ProductionOrderStatus.InProgress ? 0 : 1)
            .ThenBy(o => o.PlannedStartUtc)
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .Select(o => new
            {
                Order = o,
                VersionNumber = db.ProductVersions
                    .Where(v => v.Id == o.ProductVersionId)
                    .Select(v => v.VersionNumber)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ProductionOrderDto>(
            [.. rows.Select(r => r.Order.ToDto(r.VersionNumber))],
            paging.Page,
            paging.PageSize,
            total);
    }

    public async Task<Result<ProductionOrderDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await db.ProductionOrders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new
            {
                Order = o,
                VersionNumber = db.ProductVersions
                    .Where(v => v.Id == o.ProductVersionId)
                    .Select(v => v.VersionNumber)
                    .FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result.Failure<ProductionOrderDto>(NotFound(id))
            : Result.Success(row.Order.ToDto(row.VersionNumber));
    }

    public async Task<Result<ProductionOrderDto>> CreateAsync(
        CreateProductionOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var number = request.OrderNumber.Trim().ToUpperInvariant();

        if (await db.ProductionOrders.AnyAsync(o => o.OrderNumber == number, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProductionOrderDto>(Error.Conflict(
                "production_order.number_in_use",
                $"Order number {number} already exists."));
        }

        // A revision may only be built against once it has actually been released.
        if (request.ProductVersionId is { } versionId)
        {
            var version = await db.ProductVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken)
                .ConfigureAwait(false);

            if (version is null)
            {
                return Result.Failure<ProductionOrderDto>(Error.Validation(
                    "production_order.version_not_found",
                    $"No product revision with id {versionId}."));
            }

            if (version.Status != ProductVersionStatus.Released)
            {
                return Result.Failure<ProductionOrderDto>(Error.Validation(
                    "production_order.version_not_released",
                    $"Revision {version.VersionNumber} is {version.Status} and cannot be produced against."));
            }
        }

        try
        {
            var order = ProductionOrder.Create(
                request.OrderNumber,
                request.ProductCode,
                request.PlannedQuantity,
                request.PlannedStartUtc,
                request.PlannedEndUtc,
                request.ProductVersionId);

            db.ProductionOrders.Add(order);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Production order {OrderNumber} created.", order.OrderNumber);
            return Result.Success(order.ToDto());
        }
        catch (DomainException ex)
        {
            return Result.Failure<ProductionOrderDto>(Error.Validation(ex.Code, ex.Message));
        }
    }

    public Task<Result<ProductionOrderDto>> ReleaseAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, (order, now) => order.Release(now), cancellationToken);

    public Task<Result<ProductionOrderDto>> StartAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, (order, now) => order.Start(now), cancellationToken);

    public Task<Result<ProductionOrderDto>> PutOnHoldAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, (order, now) => order.PutOnHold(now), cancellationToken);

    public Task<Result<ProductionOrderDto>> ReportProductionAsync(
        Guid id,
        ReportProductionRequest request,
        CancellationToken cancellationToken = default) =>
        MutateAsync(id, (order, now) => order.ReportProduction(request.Quantity, now), cancellationToken);

    public Task<Result<ProductionOrderDto>> CompleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, (order, now) => order.Complete(now), cancellationToken);

    public Task<Result<ProductionOrderDto>> CancelAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, (order, now) => order.Cancel(now), cancellationToken);

    /// <summary>
    /// Loads the order, applies a state transition and saves. Every lifecycle method funnels
    /// through here so that loading, error translation and saving exist in exactly one place.
    /// </summary>
    private async Task<Result<ProductionOrderDto>> MutateAsync(
        Guid id,
        Action<ProductionOrder, DateTime> transition,
        CancellationToken cancellationToken)
    {
        var order = await db.ProductionOrders
            .SingleOrDefaultAsync(o => o.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(NotFound(id));
        }

        try
        {
            transition(order, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Production order {OrderNumber} is now {Status}.",
                order.OrderNumber,
                order.Status);

            return Result.Success(order.ToDto());
        }
        catch (DomainException ex)
        {
            // An illegal transition is the caller asking for something the order's state forbids.
            return Result.Failure<ProductionOrderDto>(Error.Conflict(ex.Code, ex.Message));
        }
    }

    private static Error NotFound(Guid id) =>
        Error.NotFound("production_order.not_found", $"No production order with id {id}.");
}
