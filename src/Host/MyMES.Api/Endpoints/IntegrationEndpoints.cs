using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyMES.Abstractions.Security;
using MyMES.Application.Integrations;
using MyMES.Contracts.Integrations;
using MyMES.PluginSystem.Http;

namespace MyMES.Api.Endpoints;

internal static class IntegrationEndpoints
{
    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/integrations/erp").WithTags("ERP integration");

        group.MapPost("/products", async (
                ReceiveErpProductRequest request,
                IErpIngestionService svc,
                CancellationToken ct) =>
            {
                var result = await svc.ReceiveAsync(request, ct);
                return result.ToCreated(m => $"/api/v1/integrations/erp/messages/{m.Id}");
            })
            .WithName("ReceiveErpProduct")
            .WithSummary("Queues an ERP product for ingestion.")
            .Produces<IncomingErpMessageDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AllowAnonymous();

        group.MapGet("/messages", async (
                IErpIngestionService svc,
                IncomingMessageStatusDto? status,
                CancellationToken ct) =>
                Results.Ok(await svc.GetAsync(status, ct)))
            .WithName("GetErpMessages")
            .WithSummary("Lists incoming ERP messages and their status.")
            .Produces<IReadOnlyList<IncomingErpMessageDto>>()
            .AllowAnonymous();

        return endpoints;
    }
}
