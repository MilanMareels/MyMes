using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMES.Abstractions.Security;
using MyMES.Application.Products;
using MyMES.Contracts.Common;
using MyMES.Contracts.Products;

namespace MyMES.Api.Controllers;

/// <summary>The <c>/products</c> API surface, including revision management.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/products")]
public sealed class ProductsController(IProductService products) : ApiControllerBase
{
    [HttpGet]
    [Authorize(MesPermissions.ProductsRead)]
    public async Task<IActionResult> List(
        int? page,
        int? pageSize,
        bool? activeOnly,
        string? search,
        CancellationToken cancellationToken)
    {
        var request = new PageRequest { Page = page ?? 1, PageSize = pageSize ?? 25 };
        return Ok(await products.GetAsync(request, activeOnly, search, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(MesPermissions.ProductsRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToOk(await products.GetByIdAsync(id, cancellationToken));

    [HttpGet("by-code/{productCode}")]
    [Authorize(MesPermissions.ProductsRead)]
    public async Task<IActionResult> GetByCode(string productCode, CancellationToken cancellationToken) =>
        ToOk(await products.GetByCodeAsync(productCode, cancellationToken));

    [HttpPost]
    [Authorize(MesPermissions.ProductsWrite)]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken cancellationToken) =>
        ToCreated(await products.CreateAsync(request, cancellationToken), p => $"/api/v1/products/{p.Id}");

    [HttpPut("{id:guid}")]
    [Authorize(MesPermissions.ProductsWrite)]
    public async Task<IActionResult> Update(Guid id, UpdateProductRequest request, CancellationToken cancellationToken) =>
        ToOk(await products.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/versions")]
    [Authorize(MesPermissions.ProductsWrite)]
    public async Task<IActionResult> AddVersion(Guid id, CreateProductVersionRequest request, CancellationToken cancellationToken) =>
        ToCreated(await products.AddVersionAsync(id, request, cancellationToken), v => $"/api/v1/products/{id}/versions/{v.Id}");

    // Releasing is its own right: drafting a revision and approving it for production are
    // different responsibilities.
    [HttpPost("{id:guid}/versions/{versionId:guid}/release")]
    [Authorize(MesPermissions.ProductsRelease)]
    public async Task<IActionResult> ReleaseVersion(Guid id, Guid versionId, CancellationToken cancellationToken) =>
        ToOk(await products.ReleaseVersionAsync(id, versionId, cancellationToken));

    [HttpPost("{id:guid}/versions/{versionId:guid}/obsolete")]
    [Authorize(MesPermissions.ProductsRelease)]
    public async Task<IActionResult> ObsoleteVersion(Guid id, Guid versionId, CancellationToken cancellationToken) =>
        ToOk(await products.ObsoleteVersionAsync(id, versionId, cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(MesPermissions.ProductsWrite)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        ToOk(await products.DeactivateAsync(id, cancellationToken));

    [HttpPost("{id:guid}/activate")]
    [Authorize(MesPermissions.ProductsWrite)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken) =>
        ToOk(await products.ActivateAsync(id, cancellationToken));
}
