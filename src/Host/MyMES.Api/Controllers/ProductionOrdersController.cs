using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMES.Abstractions.Security;
using MyMES.Application.Production;
using MyMES.Contracts.Common;
using MyMES.Contracts.Production;

namespace MyMES.Api.Controllers;

/// <summary>The <c>/production-orders</c> API surface.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/production-orders")]
public sealed class ProductionOrdersController(IProductionOrderService orders) : ApiControllerBase
{
    [HttpGet]
    [Authorize(MesPermissions.ProductionRead)]
    public async Task<IActionResult> List(
        int? page,
        int? pageSize,
        ProductionOrderStatusDto? status,
        string? search,
        CancellationToken cancellationToken)
    {
        var request = new PageRequest { Page = page ?? 1, PageSize = pageSize ?? 25 };
        return Ok(await orders.GetAsync(request, status, search, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(MesPermissions.ProductionRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToOk(await orders.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(MesPermissions.ProductionWrite)]
    public async Task<IActionResult> Create(CreateProductionOrderRequest request, CancellationToken cancellationToken) =>
        ToCreated(await orders.CreateAsync(request, cancellationToken), o => $"/api/v1/production-orders/{o.Id}");

    [HttpPost("{id:guid}/release")]
    [Authorize(MesPermissions.ProductionWrite)]
    public async Task<IActionResult> Release(Guid id, CancellationToken cancellationToken) =>
        ToOk(await orders.ReleaseAsync(id, cancellationToken));

    [HttpPost("{id:guid}/start")]
    [Authorize(MesPermissions.ProductionExecute)]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken) =>
        ToOk(await orders.StartAsync(id, cancellationToken));

    [HttpPost("{id:guid}/hold")]
    [Authorize(MesPermissions.ProductionExecute)]
    public async Task<IActionResult> Hold(Guid id, CancellationToken cancellationToken) =>
        ToOk(await orders.PutOnHoldAsync(id, cancellationToken));

    [HttpPost("{id:guid}/report")]
    [Authorize(MesPermissions.ProductionExecute)]
    public async Task<IActionResult> Report(Guid id, ReportProductionRequest request, CancellationToken cancellationToken) =>
        ToOk(await orders.ReportProductionAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/complete")]
    [Authorize(MesPermissions.ProductionExecute)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken) =>
        ToOk(await orders.CompleteAsync(id, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(MesPermissions.ProductionWrite)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        ToOk(await orders.CancelAsync(id, cancellationToken));
}
