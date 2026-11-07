using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMES.Abstractions.Results;
using MyMES.Abstractions.Time;
using MyMES.Application.Abstractions;
using MyMES.Application.Mapping;
using MyMES.Contracts.Common;
using MyMES.Contracts.Production;
using MyMES.Domain.Production;
using MyMES.Domain.Products;
using MyMES.EventBus;

namespace MyMES.Application.Production;

/// <inheritdoc cref="IProductionOrderService"/>
public sealed class ProductionOrderService(
    IApplicationDbContext db,
    IDateTimeProvider clock,
    IEventBus eventBus,
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
        var productCode = request.ProductCode.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(number))
        {
            return Result.Failure<ProductionOrderDto>(Error.Validation(
                "production_order.number_required", "An order needs a number."));
        }

        if (string.IsNullOrWhiteSpace(productCode))
        {
            return Result.Failure<ProductionOrderDto>(Error.Validation(
                "production_order.product_required", "An order needs a product code."));
        }

        if (request.PlannedQuantity <= 0)
        {
            return Result.Failure<ProductionOrderDto>(Error.Validation(
                "production_order.quantity_invalid", "Planned quantity must be greater than zero."));
        }

        if (request.PlannedEndUtc < request.PlannedStartUtc)
        {
            return Result.Failure<ProductionOrderDto>(Error.Validation(
                "production_order.dates_invalid", "Planned end cannot be before planned start."));
        }

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

        var order = new ProductionOrder(Guid.NewGuid())
        {
            OrderNumber = number,
            ProductCode = productCode,
            ProductVersionId = request.ProductVersionId,
            PlannedQuantity = request.PlannedQuantity,
            PlannedStartUtc = request.PlannedStartUtc,
            PlannedEndUtc = request.PlannedEndUtc,
            Status = ProductionOrderStatus.Draft
        };

        db.ProductionOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Production order {OrderNumber} created.", order.OrderNumber);
        return Result.Success(order.ToDto());
    }

    public async Task<Result<ProductionOrderDto>> ReleaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(NotFound(id));
        }

        if (order.Status != ProductionOrderStatus.Draft)
        {
            return Result.Failure<ProductionOrderDto>(Error.Conflict(
                "production_order.not_draft",
                $"Order {order.OrderNumber} is {order.Status} and cannot be released again."));
        }

        order.Status = ProductionOrderStatus.Released;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await eventBus.PublishAsync(
            new ProductionOrderReleasedEvent(order.Id, order.OrderNumber, order.ProductCode, clock.UtcNow),
            cancellationToken).ConfigureAwait(false);

        return Result.Success(order.ToDto());
    }

    public async Task<Result<ProductionOrderDto>> StartAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(NotFound(id));
        }

        if (order.Status is not (ProductionOrderStatus.Released or ProductionOrderStatus.OnHold))
        {
            return Result.Failure<ProductionOrderDto>(Error.Conflict(
                "production_order.not_startable",
                $"Order {order.OrderNumber} is {order.Status} and cannot be started."));
        }

        order.Status = ProductionOrderStatus.InProgress;
        order.ActualStartUtc ??= clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await eventBus.PublishAsync(
            new ProductionOrderStartedEvent(order.Id, order.OrderNumber, clock.UtcNow),
            cancellationToken).ConfigureAwait(false);

        return Result.Success(order.ToDto());
    }

    public async Task<Result<ProductionOrderDto>> PutOnHoldAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(NotFound(id));
        }

        if (order.Status != ProductionOrderStatus.InProgress)
        {
            return Result.Failure<ProductionOrderDto>(Error.Conflict(
                "production_order.not_running",
                $"Order {order.OrderNumber} is {order.Status} and cannot be put on hold."));
        }

        order.Status = ProductionOrderStatus.OnHold;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(order.ToDto());
    }

    public async Task<Result<ProductionOrderDto>> ReportProductionAsync(
        Guid id,
        ReportProductionRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(NotFound(id));
        }

        if (request.Quantity <= 0)
        {
            return Result.Failure<ProductionOrderDto>(Error.Validation(
                "production_order.quantity_invalid", "Reported quantity must be greater than zero."));
        }

        if (order.Status != ProductionOrderStatus.InProgress)
        {
            return Result.Failure<ProductionOrderDto>(Error.Conflict(
                "production_order.not_running",
                $"Order {order.OrderNumber} is {order.Status}; production can only be booked while it runs."));
        }

        order.ProducedQuantity += request.Quantity;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await eventBus.PublishAsync(
            new ProductionReportedEvent(order.Id, order.OrderNumber, request.Quantity, order.ProducedQuantity, clock.UtcNow),
            cancellationToken).ConfigureAwait(false);

        return Result.Success(order.ToDto());
    }

    public async Task<Result<ProductionOrderDto>> CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(NotFound(id));
        }

        if (order.Status is not (ProductionOrderStatus.InProgress or ProductionOrderStatus.OnHold))
        {
            return Result.Failure<ProductionOrderDto>(Error.Conflict(
                "production_order.not_completable",
                $"Order {order.OrderNumber} is {order.Status} and cannot be completed."));
        }

        order.Status = ProductionOrderStatus.Completed;
        order.ActualEndUtc = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await eventBus.PublishAsync(
            new ProductionOrderCompletedEvent(order.Id, order.OrderNumber, order.ProducedQuantity, clock.UtcNow),
            cancellationToken).ConfigureAwait(false);

        return Result.Success(order.ToDto());
    }

    public async Task<Result<ProductionOrderDto>> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return Result.Failure<ProductionOrderDto>(NotFound(id));
        }

        if (order.IsClosed)
        {
            return Result.Failure<ProductionOrderDto>(Error.Conflict(
                "production_order.closed",
                $"Order {order.OrderNumber} is already {order.Status}."));
        }

        order.Status = ProductionOrderStatus.Cancelled;
        order.ActualEndUtc = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(order.ToDto());
    }

    private Task<ProductionOrder?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        db.ProductionOrders.SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    private static Error NotFound(Guid id) =>
        Error.NotFound("production_order.not_found", $"No production order with id {id}.");
}
