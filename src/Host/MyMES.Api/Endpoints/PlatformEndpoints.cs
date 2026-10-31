using MyMES.Application.Dashboard;
using MyMES.Abstractions.Security;
using MyMES.Contracts.Dashboard;
using MyMES.PluginSystem;

namespace MyMES.Api.Endpoints;

/// <summary>Dashboard figures and the platform's own inventory of what it loaded.</summary>
internal static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/dashboard/summary", async (
                IDashboardService dashboard,
                CancellationToken cancellationToken) =>
                Results.Ok(await dashboard.GetSummaryAsync(cancellationToken)))
            .WithTags("Dashboard")
            .WithName("GetDashboardSummary")
            .WithSummary("The figures behind the dashboard KPI cards.")
            .Produces<DashboardSummaryDto>()
            // Any signed-in user may see the plant's headline numbers.
            .RequireAuthorization();

        endpoints.MapGet("/platform", (
                IModuleRegistry registry,
                IHostEnvironment environment) =>
            {
                var version = typeof(PlatformEndpoints).Assembly.GetName().Version?.ToString() ?? "0.0.0";

                return Results.Ok(new PlatformInfoDto(
                    version,
                    environment.EnvironmentName,
                    [.. registry.Modules.Select(m => new ModuleInfoDto(m.Name, m.Order, m.AssemblyName))],
                    registry.Plugins));
            })
            .WithTags("Platform")
            .WithName("GetPlatformInfo")
            .WithSummary("Which modules and plugins this instance is running.")
            .Produces<PlatformInfoDto>()
            .RequireAuthorization(MesPermissions.UsersRead);

        return endpoints;
    }
}
