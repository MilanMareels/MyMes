namespace MyMES.Contracts.Dashboard;

/// <summary>
/// The figures behind the dashboard KPI cards, assembled server-side in one round trip.
/// </summary>
public sealed record DashboardSummaryDto(
    int TotalMachines,
    int RunningMachines,
    int FaultedMachines,
    int ActiveProductionOrders,
    decimal ProducedQuantityToday,
    int CompletedOrdersToday,
    int ActiveProducts,
    DateTime GeneratedAtUtc);

/// <summary>A module the host has loaded, as reported by the diagnostics endpoint.</summary>
public sealed record ModuleInfoDto(string Name, int Order, string Assembly);

/// <summary>Everything the host loaded at start-up: modules first, then plugins.</summary>
public sealed record PlatformInfoDto(
    string Version,
    string Environment,
    IReadOnlyList<ModuleInfoDto> Modules,
    IReadOnlyList<string> Plugins);
