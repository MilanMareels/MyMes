using Microsoft.EntityFrameworkCore;
using MyMES.Application.Time;
using MyMES.Application.Data;
using MyMES.Application.Dashboard;
using MyMES.Domain.Production;
using MyMES.Domain.Products;

namespace MyMES.Application.Dashboard;

/// <summary>Supplies the aggregate figures shown on the dashboard.</summary>
public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IDashboardService"/>
public sealed class DashboardService(
    IRepository<ProductionOrder> orders,
    IRepository<Product> products,
    IDateTimeProvider clock) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var startOfDay = now.Date;

        // Counted with aggregate queries rather than by loading rows: these numbers are polled
        // from the dashboard and must stay cheap as the tables grow.
        var activeOrders = await orders.Query()
            .CountAsync(
                o => o.Status == ProductionOrderStatus.Released
                     || o.Status == ProductionOrderStatus.InProgress
                     || o.Status == ProductionOrderStatus.OnHold,
                cancellationToken)
            .ConfigureAwait(false);

        var producedToday = await orders.Query()
            .Where(o => o.UpdatedAtUtc != null && o.UpdatedAtUtc >= startOfDay)
            .SumAsync(o => (decimal?)o.ProducedQuantity, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        var completedToday = await orders.Query()
            .CountAsync(
                o => o.Status == ProductionOrderStatus.Completed
                     && o.ActualEndUtc != null
                     && o.ActualEndUtc >= startOfDay,
                cancellationToken)
            .ConfigureAwait(false);

        var activeProducts = await products.Query()
            .CountAsync(p => p.IsActive, cancellationToken)
            .ConfigureAwait(false);

        return new DashboardSummaryDto(
            activeOrders,
            producedToday,
            completedToday,
            activeProducts,
            now);
    }
}
