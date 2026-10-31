using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyMES.Abstractions.Modules;
using MyMES.Domain.Products;
using MyMES.EventBus;
using MyMES.Modules.Production.Endpoints;
using MyMES.Modules.Production.Handlers;
using MyMES.PluginSystem;

namespace MyMES.Modules.Production;

/// <summary>
/// Production orders and the product catalogue they are built against.
/// </summary>
/// <remarks>
/// Owns <c>/production-orders</c> and <c>/products</c>, and reacts to a released revision by
/// recording that the plant's current build standard has moved.
/// </remarks>
public sealed class ProductionModule : IMyMesModule, IEndpointModule
{
    public string Name => "Production";

    /// <summary>Runs after Machines, whose state production reads.</summary>
    public int Order => 20;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // The module's own reaction to a domain event. Registered here, not in the host, so the
        // module stays self-contained.
        services.AddEventHandler<ProductVersionReleasedEvent, ProductVersionReleasedHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapProductionOrderEndpoints();
        endpoints.MapProductEndpoints();
    }
}
