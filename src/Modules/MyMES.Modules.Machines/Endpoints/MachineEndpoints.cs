using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyMES.Application.Machines;
using MyMES.Contracts.Common;
using MyMES.Abstractions.Security;
using MyMES.Contracts.Machines;
using MyMES.PluginSystem.Http;

namespace MyMES.Modules.Machines.Endpoints;

/// <summary>The <c>/machines</c> API surface.</summary>
internal static class MachineEndpoints
{
    public static IEndpointRouteBuilder MapMachineEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/machines")
            .WithTags("Machines");

        group.MapGet("/", async (
                IMachineService machines,
                int? page,
                int? pageSize,
                MachineStatusDto? status,
                string? search,
                CancellationToken cancellationToken) =>
            {
                var request = new PageRequest { Page = page ?? 1, PageSize = pageSize ?? 25 };
                var result = await machines.GetAsync(request, status, search, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("GetMachines")
            .WithSummary("Lists machines, optionally filtered by status or a search term.")
            .Produces<PagedResult<MachineDto>>()
            .RequireAuthorization(MesPermissions.MachinesRead);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IMachineService machines,
                CancellationToken cancellationToken) =>
            {
                var result = await machines.GetByIdAsync(id, cancellationToken);
                return result.ToOk();
            })
            .WithName("GetMachineById")
            .WithSummary("Returns a single machine.")
            .Produces<MachineDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(MesPermissions.MachinesRead);

        group.MapPost("/", async (
                CreateMachineRequest request,
                IMachineService machines,
                CancellationToken cancellationToken) =>
            {
                var result = await machines.CreateAsync(request, cancellationToken);
                return result.ToCreated(m => $"/api/v1/machines/{m.Id}");
            })
            .WithName("CreateMachine")
            .WithSummary("Registers a new machine.")
            .Produces<MachineDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MesPermissions.MachinesWrite);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateMachineRequest request,
                IMachineService machines,
                CancellationToken cancellationToken) =>
            {
                var result = await machines.UpdateAsync(id, request, cancellationToken);
                return result.ToOk();
            })
            .WithName("UpdateMachine")
            .WithSummary("Updates a machine's name and description.")
            .Produces<MachineDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(MesPermissions.MachinesWrite);

        // Separate from the update endpoint: changing state is a different right, held by
        // operators and maintenance who may not edit the register itself.
        group.MapPost("/{id:guid}/status", async (
                Guid id,
                ChangeMachineStatusRequest request,
                IMachineService machines,
                CancellationToken cancellationToken) =>
            {
                var result = await machines.ChangeStatusAsync(id, request, cancellationToken);
                return result.ToOk();
            })
            .WithName("ChangeMachineStatus")
            .WithSummary("Moves a machine to a new operational status.")
            .Produces<MachineDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MesPermissions.MachinesChangeStatus);

        group.MapPost("/{id:guid}/decommission", async (
                Guid id,
                IMachineService machines,
                CancellationToken cancellationToken) =>
            {
                var result = await machines.DecommissionAsync(id, cancellationToken);
                return result.ToOk();
            })
            .WithName("DecommissionMachine")
            .WithSummary("Permanently withdraws a machine from service.")
            .Produces<MachineDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(MesPermissions.MachinesWrite);

        return endpoints;
    }
}
