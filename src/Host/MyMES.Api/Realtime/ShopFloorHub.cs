using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MyMES.Abstractions.Security;
using MyMES.Contracts.Machines;
using MyMES.Contracts.Production;
using MyMES.Messaging;

namespace MyMES.Api.Realtime;

/// <summary>
/// Real-time channel for shop-floor changes.
/// </summary>
/// <remarks>
/// Authenticated like every other endpoint: a hub connection is an API call that stays open, and
/// leaving it anonymous would be a way around the permission model.
/// </remarks>
[Authorize]
public sealed class ShopFloorHub : Hub
{
    /// <summary>Narrows a client's traffic to one machine, for a terminal mounted on it.</summary>
    public Task SubscribeToMachine(string machineCode) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(machineCode));

    public Task UnsubscribeFromMachine(string machineCode) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(machineCode));

    internal static string GroupFor(string machineCode) => $"machine:{machineCode}";
}

/// <summary>Publishes changes over SignalR.</summary>
public sealed class SignalRRealtimeNotifier(IHubContext<ShopFloorHub> hub) : IRealtimeNotifier
{
    public const string MachineChanged = "MachineChanged";
    public const string ProductionOrderChanged = "ProductionOrderChanged";
    public const string Telemetry = "Telemetry";

    public Task MachineChangedAsync(MachineDto machine, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(machine);

        // Everyone gets the plant-wide feed; the per-machine group is for terminals that only
        // care about one resource and should not pay for the rest.
        return Task.WhenAll(
            hub.Clients.All.SendAsync(MachineChanged, machine, cancellationToken),
            hub.Clients.Group(ShopFloorHub.GroupFor(machine.MachineCode))
                .SendAsync(MachineChanged, machine, cancellationToken));
    }

    public Task ProductionOrderChangedAsync(ProductionOrderDto order, CancellationToken cancellationToken = default) =>
        hub.Clients.All.SendAsync(ProductionOrderChanged, order, cancellationToken);

    public Task TelemetryAsync(MachineTelemetryReceived telemetry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        // Telemetry is high volume: only the machine's own subscribers get it.
        return hub.Clients.Group(ShopFloorHub.GroupFor(telemetry.MachineCode))
            .SendAsync(Telemetry, telemetry, cancellationToken);
    }
}
