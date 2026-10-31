using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMES.Abstractions.Modules;
using MyMES.Abstractions.Security;
using MyMES.Domain.Production;
using MyMES.EventBus;
using MyMES.PluginSystem;

namespace MyMES.Modules.Inventory;

/// <summary>
/// Stock movements caused by production.
/// </summary>
/// <remarks>
/// Scaffolding, with one real behaviour: every booking against an order produces a stock receipt.
/// It reaches that conclusion from a domain event alone, which is the rule the modular monolith
/// runs on. A full implementation adds the counterpart issue of components from the bill of
/// materials, and persists the ledger.
/// </remarks>
public sealed class InventoryModule : IMyMesModule, IEndpointModule
{
    public string Name => "Inventory";

    public int Order => 40;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IStockLedger, InMemoryStockLedger>();
        services.AddEventHandler<ProductionReportedEvent, ProductionReceiptPoster>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/inventory").WithTags("Inventory");

        group.MapGet("/movements", (IStockLedger ledger) => Results.Ok(ledger.Snapshot()))
            .WithName("GetStockMovements")
            .WithSummary("Stock movements recorded since the application started.")
            .Produces<IReadOnlyList<StockMovement>>()
            .RequireAuthorization(MesPermissions.InventoryRead);
    }
}

/// <summary>A quantity moving into or out of stock.</summary>
public sealed record StockMovement(
    Guid ProductionOrderId,
    string OrderNumber,
    decimal Quantity,
    DateTime OccurredAtUtc);

/// <summary>Records stock movements. In-memory for now; a table once the module is built out.</summary>
public interface IStockLedger
{
    void Post(StockMovement movement);

    IReadOnlyList<StockMovement> Snapshot();
}

internal sealed class InMemoryStockLedger : IStockLedger
{
    private readonly ConcurrentQueue<StockMovement> _movements = new();

    public void Post(StockMovement movement) => _movements.Enqueue(movement);

    public IReadOnlyList<StockMovement> Snapshot() => [.. _movements];
}

/// <summary>Turns a production booking into a stock receipt.</summary>
internal sealed class ProductionReceiptPoster(
    IStockLedger ledger,
    ILogger<ProductionReceiptPoster> logger) : IEventHandler<ProductionReportedEvent>
{
    public Task HandleAsync(ProductionReportedEvent message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        ledger.Post(new StockMovement(
            message.ProductionOrderId,
            message.OrderNumber,
            message.Quantity,
            message.OccurredAtUtc));

        logger.LogInformation(
            "Stock receipt of {Quantity} posted for order {OrderNumber}.",
            message.Quantity,
            message.OrderNumber);

        return Task.CompletedTask;
    }
}
