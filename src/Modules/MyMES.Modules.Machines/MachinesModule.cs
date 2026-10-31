using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyMES.Abstractions.Modules;
using MyMES.Application.Machines;
using MyMES.Contracts.Common;
using MyMES.Contracts.Machines;
using MyMES.Modules.Machines.Endpoints;
using MyMES.PluginSystem;

namespace MyMES.Modules.Machines;

/// <summary>
/// The machine register: which resources exist and what state they are in.
/// </summary>
/// <remarks>
/// Owns the <c>/machines</c> API surface. The use cases themselves live in the application layer,
/// because the Blazor UI and the worker service reach them without going through this module.
/// </remarks>
public sealed class MachinesModule : IMyMesModule, IEndpointModule
{
    public string Name => "Machines";

    /// <summary>Runs early: other modules read machine state, none of them write it.</summary>
    public int Order => 10;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // The application layer already registers IMachineService. A module registers what is
        // genuinely its own; re-registering shared services here would just invite conflicts.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapMachineEndpoints();
}
