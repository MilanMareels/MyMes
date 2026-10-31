using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyMES.Abstractions.Security;
using MyMES.Application.Production;
using MyMES.Contracts.Common;
using MyMES.Contracts.Production;
using MyMES.PluginSystem.Http;

namespace MyMES.Modules.Production.Endpoints;

/// <summary>The <c>/production-orders</c> API surface.</summary>
internal static class ProductionOrderEndpoints
{
    public static IEndpointRouteBuilder MapProductionOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/production-orders")
            .WithTags("Production orders");

        group.MapGet("/", async (
                IProductionOrderService orders,
                int? page,
                int? pageSize,
                ProductionOrderStatusDto? status,
                string? search,
                CancellationToken cancellationToken) =>
            {
                var request = new PageRequest { Page = page ?? 1, PageSize = pageSize ?? 25 };
                return Results.Ok(await orders.GetAsync(request, status, search, cancellationToken));
            })
            .WithName("GetProductionOrders")
            .WithSummary("Lists production orders, running ones first.")
            .Produces<PagedResult<ProductionOrderDto>>()
            .RequireAuthorization(MesPermissions.ProductionRead);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IProductionOrderService orders,
                CancellationToken cancellationToken) =>
                (await orders.GetByIdAsync(id, cancellationToken)).ToOk())
            .WithName("GetProductionOrderById")
            .WithSummary("Returns a single production order.")
            .Produces<ProductionOrderDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(MesPermissions.ProductionRead);

        group.MapPost("/", async (
                CreateProductionOrderRequest request,
                IProductionOrderService orders,
                CancellationToken cancellationToken) =>
                (await orders.CreateAsync(request, cancellationToken))
                    .ToCreated(o => $"/api/v1/production-orders/{o.Id}"))
            .WithName("CreateProductionOrder")
            .WithSummary("Creates a production order in draft.")
            .Produces<ProductionOrderDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MesPermissions.ProductionWrite);

        // Lifecycle transitions are separate endpoints rather than a PATCH on status. They are
        // distinct actions with distinct rights, and a verb in the URL documents that plainly.
        group.MapPost("/{id:guid}/release", async (
                Guid id,
                IProductionOrderService orders,
                CancellationToken cancellationToken) =>
                (await orders.ReleaseAsync(id, cancellationToken)).ToOk())
            .WithName("ReleaseProductionOrder")
            .WithSummary("Hands the order to the shop floor.")
            .Produces<ProductionOrderDto>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MesPermissions.ProductionWrite);

        group.MapPost("/{id:guid}/start", async (
                Guid id,
                IProductionOrderService orders,
                CancellationToken cancellationToken) =>
                (await orders.StartAsync(id, cancellationToken)).ToOk())
            .WithName("StartProductionOrder")
            .WithSummary("Starts the order, or resumes it after a hold.")
            .Produces<ProductionOrderDto>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MesPermissions.ProductionExecute);

        group.MapPost("/{id:guid}/hold", async (
                Guid id,
                IProductionOrderService orders,
                CancellationToken cancellationToken) =>
                (await orders.PutOnHoldAsync(id, cancellationToken)).ToOk())
            .WithName("HoldProductionOrder")
            .WithSummary("Pauses a running order.")
            .Produces<ProductionOrderDto>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MesPermissions.ProductionExecute);

        group.MapPost("/{id:guid}/report", async (
                Guid id,
                ReportProductionRequest request,
                IProductionOrderService orders,
                CancellationToken cancellationToken) =>
                (await orders.ReportProductionAsync(id, request, cancellationToken)).ToOk())
            .WithName("ReportProduction")
            .WithSummary("Books produced quantity against a running order.")
            .Produces<ProductionOrderDto>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MesPermissions.ProductionExecute);

        group.MapPost("/{id:guid}/complete", async (
                Guid id,
                IProductionOrderService orders,
                CancellationToken cancellationToken) =>
                (await orders.CompleteAsync(id, cancellationToken)).ToOk())
            .WithName("CompleteProductionOrder")
            .WithSummary("Closes the order as finished.")
            .Produces<ProductionOrderDto>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MesPermissions.ProductionExecute);

        group.MapPost("/{id:guid}/cancel", async (
                Guid id,
                IProductionOrderService orders,
                CancellationToken cancellationToken) =>
                (await orders.CancelAsync(id, cancellationToken)).ToOk())
            .WithName("CancelProductionOrder")
            .WithSummary("Abandons the order.")
            .Produces<ProductionOrderDto>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MesPermissions.ProductionWrite);

        return endpoints;
    }
}
