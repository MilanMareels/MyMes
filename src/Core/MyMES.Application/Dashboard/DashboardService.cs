using Microsoft.EntityFrameworkCore;
using MyMES.Abstractions.Time;
using MyMES.Application.Abstractions;
using MyMES.Contracts.Dashboard;
using MyMES.Domain.Production;

namespace MyMES.Application.Dashboard;

/// <summary>Supplies the aggregate figures shown on the dashboard.</summary>
public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IDashboardService"/>
public sealed class DashboardService(IApplicationDbContext db, IDateTimeProvider clock) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var startOfDay = now.Date;

        // Counted with aggregate queries rather than by loading rows: these numbers are polled
        // from the dashboard and must stay cheap as the tables grow.
        var activeOrders = await db.ProductionOrders
            .CountAsync(
                o => o.Status == ProductionOrderStatus.Released
                     || o.Status == ProductionOrderStatus.InProgress
                     || o.Status == ProductionOrderStatus.OnHold,
                cancellationToken)
            .ConfigureAwait(false);

        // "Today" means orders touched today; a booking-level ledger comes with the Production
        // module's reporting work, and this stays honest until then.
        var producedToday = await db.ProductionOrders
            .Where(o => o.UpdatedAtUtc != null && o.UpdatedAtUtc >= startOfDay)
            .SumAsync(o => (decimal?)o.ProducedQuantity, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        var completedToday = await db.ProductionOrders
            .CountAsync(
                o => o.Status == ProductionOrderStatus.Completed
                     && o.ActualEndUtc != null
                     && o.ActualEndUtc >= startOfDay,
                cancellationToken)
            .ConfigureAwait(false);

        var activeProducts = await db.Products
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
