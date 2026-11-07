using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMES.Application.Integrations;

namespace MyMES.Api.Controllers;

/// <summary>Ingestion of products from an external ERP.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/integrations/erp")]
public sealed class IntegrationController(IErpIngestionService service) : ControllerBase
{
    [HttpPost("products")]
    [AllowAnonymous]
    public async Task<IActionResult> Receive(ReceiveErpProductRequest request, CancellationToken cancellationToken)
    {
        var result = await service.ReceiveAsync(request, cancellationToken);
        return result.IsSuccess
            ? Created($"/api/v1/integrations/erp/messages/{result.Value.Id}", result.Value)
            : Problem(
                title: "Conflict",
                detail: result.Error!.Value.Message,
                statusCode: StatusCodes.Status409Conflict);
    }

    [HttpGet("messages")]
    [AllowAnonymous]
    public async Task<IActionResult> Messages(IncomingMessageStatusDto? status, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(status, cancellationToken));
}
