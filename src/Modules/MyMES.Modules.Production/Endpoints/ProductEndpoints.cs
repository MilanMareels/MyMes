using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyMES.Abstractions.Security;
using MyMES.Application.Products;
using MyMES.Contracts.Common;
using MyMES.Contracts.Products;
using MyMES.PluginSystem.Http;

namespace MyMES.Modules.Production.Endpoints;

/// <summary>
/// The <c>/products</c> API surface, including revision management.
/// </summary>
/// <remarks>
/// Products sit in the Production module because a revision only means something in the context
/// of what is being built. If product data ever grows its own lifecycle (engineering change
/// orders, approvals), it earns a module of its own.
/// </remarks>
internal static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/products")
            .WithTags("Products");

        group.MapGet("/", async (
                IProductService products,
                int? page,
                int? pageSize,
                bool? activeOnly,
                string? search,
                CancellationToken cancellationToken) =>
            {
                var request = new PageRequest { Page = page ?? 1, PageSize = pageSize ?? 25 };
                return Results.Ok(await products.GetAsync(request, activeOnly, search, cancellationToken));
            })
            .WithName("GetProducts")
            .WithSummary("Lists products with their released revision.")
            .Produces<PagedResult<ProductSummaryDto>>()
            .RequireAuthorization(MesPermissions.ProductsRead);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IProductService products,
                CancellationToken cancellationToken) =>
                (await products.GetByIdAsync(id, cancellationToken)).ToOk())
            .WithName("GetProductById")
            .WithSummary("Returns a product with its full revision history.")
            .Produces<ProductDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(MesPermissions.ProductsRead);

        group.MapGet("/by-code/{productCode}", async (
                string productCode,
                IProductService products,
                CancellationToken cancellationToken) =>
                (await products.GetByCodeAsync(productCode, cancellationToken)).ToOk())
            .WithName("GetProductByCode")
            .WithSummary("Looks a product up by its business key.")
            .Produces<ProductDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(MesPermissions.ProductsRead);

        group.MapPost("/", async (
                CreateProductRequest request,
                IProductService products,
                CancellationToken cancellationToken) =>
                (await products.CreateAsync(request, cancellationToken))
                    .ToCreated(p => $"/api/v1/products/{p.Id}"))
            .WithName("CreateProduct")
            .WithSummary("Creates a product, optionally with its first draft revision.")
            .Produces<ProductDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MesPermissions.ProductsWrite);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateProductRequest request,
                IProductService products,
                CancellationToken cancellationToken) =>
                (await products.UpdateAsync(id, request, cancellationToken)).ToOk())
            .WithName("UpdateProduct")
            .WithSummary("Updates a product's descriptive fields.")
            .Produces<ProductDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(MesPermissions.ProductsWrite);

        group.MapPost("/{id:guid}/versions", async (
                Guid id,
                AddProductVersionRequest request,
                IProductService products,
                CancellationToken cancellationToken) =>
                (await products.AddVersionAsync(id, request, cancellationToken))
                    .ToCreated(v => $"/api/v1/products/{id}/versions/{v.Id}"))
            .WithName("AddProductVersion")
            .WithSummary("Adds a draft revision to a product.")
            .Produces<ProductVersionDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MesPermissions.ProductsWrite);

        // Releasing is its own right: drafting a revision and approving it for production are
        // different responsibilities, and in a regulated plant they are different people.
        group.MapPost("/{id:guid}/versions/{versionId:guid}/release", async (
                Guid id,
                Guid versionId,
                IProductService products,
                CancellationToken cancellationToken) =>
                (await products.ReleaseVersionAsync(id, versionId, cancellationToken)).ToOk())
            .WithName("ReleaseProductVersion")
            .WithSummary("Promotes a draft revision to released, retiring the previous one.")
            .Produces<ProductDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MesPermissions.ProductsRelease);

        group.MapPost("/{id:guid}/versions/{versionId:guid}/obsolete", async (
                Guid id,
                Guid versionId,
                IProductService products,
                CancellationToken cancellationToken) =>
                (await products.ObsoleteVersionAsync(id, versionId, cancellationToken)).ToOk())
            .WithName("ObsoleteProductVersion")
            .WithSummary("Withdraws a revision without replacing it.")
            .Produces<ProductDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(MesPermissions.ProductsRelease);

        group.MapPost("/{id:guid}/deactivate", async (
                Guid id,
                IProductService products,
                CancellationToken cancellationToken) =>
                (await products.DeactivateAsync(id, cancellationToken)).ToOk())
            .WithName("DeactivateProduct")
            .WithSummary("Deactivates a product and retires its released revision.")
            .Produces<ProductDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(MesPermissions.ProductsWrite);

        return endpoints;
    }
}
