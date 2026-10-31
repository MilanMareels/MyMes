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

namespace MyMES.Modules.Quality;

/// <summary>
/// Inspection and release of finished work.
/// </summary>
/// <remarks>
/// Scaffolding at this stage, but not empty: it demonstrates the pattern every later feature will
/// follow. It reacts to a completed order by raising the inspection that has to happen before the
/// goods can ship, and it does so without referencing the Production module at all. The only
/// thing shared is the domain event.
/// </remarks>
public sealed class QualityModule : IMyMesModule, IEndpointModule
{
    public string Name => "Quality";

    public int Order => 30;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Singleton because the in-memory queue has to outlive the request that filled it.
        services.AddSingleton<IInspectionBacklog, InMemoryInspectionBacklog>();
        services.AddEventHandler<ProductionOrderCompletedEvent, FinalInspectionTrigger>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/quality").WithTags("Quality");

        group.MapGet("/pending-inspections", (IInspectionBacklog backlog) =>
                Results.Ok(backlog.Snapshot()))
            .WithName("GetPendingInspections")
            .WithSummary("Orders that finished production and still await final inspection.")
            .Produces<IReadOnlyList<PendingInspection>>()
            .RequireAuthorization(MesPermissions.QualityRead);
    }
}

/// <summary>An order waiting for its final inspection.</summary>
public sealed record PendingInspection(Guid ProductionOrderId, string OrderNumber, DateTime RaisedAtUtc);

/// <summary>
/// Holds the inspections raised so far.
/// </summary>
/// <remarks>
/// Backed by memory and therefore lost on restart. That is adequate for the foundation and
/// deliberately obvious: the real implementation is a table, and replacing this registration is
/// the whole change.
/// </remarks>
public interface IInspectionBacklog
{
    void Add(PendingInspection inspection);

    IReadOnlyList<PendingInspection> Snapshot();
}

internal sealed class InMemoryInspectionBacklog : IInspectionBacklog
{
    private readonly List<PendingInspection> _items = [];
    private readonly Lock _gate = new();

    public void Add(PendingInspection inspection)
    {
        lock (_gate)
        {
            _items.Add(inspection);
        }
    }

    public IReadOnlyList<PendingInspection> Snapshot()
    {
        lock (_gate)
        {
            return [.. _items];
        }
    }
}

/// <summary>Raises a final inspection whenever an order completes.</summary>
internal sealed class FinalInspectionTrigger(
    IInspectionBacklog backlog,
    ILogger<FinalInspectionTrigger> logger) : IEventHandler<ProductionOrderCompletedEvent>
{
    public Task HandleAsync(ProductionOrderCompletedEvent message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        backlog.Add(new PendingInspection(message.ProductionOrderId, message.OrderNumber, message.OccurredAtUtc));

        logger.LogInformation(
            "Final inspection raised for completed order {OrderNumber} ({Quantity} produced).",
            message.OrderNumber,
            message.ProducedQuantity);

        return Task.CompletedTask;
    }
}

