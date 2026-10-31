using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MyMES.Application.Dashboard;
using MyMES.Application.Integrations;
using MyMES.Application.Machines;
using MyMES.Application.Production;
using MyMES.Application.Products;
using MyMES.EventBus;

namespace MyMES.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the application layer: use-case services, validators and the event bus.
    /// </summary>
    /// <remarks>
    /// Services are scoped because they hold the <c>IApplicationDbContext</c> for the duration of
    /// one request or one background work item.
    /// </remarks>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMachineService, MachineService>();
        services.AddScoped<IProductionOrderService, ProductionOrderService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IErpIngestionService, ErpIngestionService>();

        services.AddValidatorsFromAssemblyContaining<MyMesApplicationMarker>(ServiceLifetime.Singleton);
        services.AddInProcessEventBus();

        return services;
    }
}

/// <summary>
/// Anchors assembly scanning to this project without depending on a concrete service name.
/// </summary>
public sealed class MyMesApplicationMarker
{
    private MyMesApplicationMarker()
    {
    }
}
