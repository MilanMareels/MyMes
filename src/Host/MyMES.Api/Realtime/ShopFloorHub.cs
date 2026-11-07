using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MyMES.Contracts.Production;
using MyMES.Messaging;

namespace MyMES.Api.Realtime;

/// <summary>
/// Real-time channel for shop-floor changes.
/// </summary>
[Authorize]
public sealed class ShopFloorHub : Hub
{
}

/// <summary>Publishes changes over SignalR.</summary>
public sealed class SignalRRealtimeNotifier(IHubContext<ShopFloorHub> hub) : IRealtimeNotifier
{
    public const string ProductionOrderChanged = "ProductionOrderChanged";

    public Task ProductionOrderChangedAsync(ProductionOrderDto order, CancellationToken cancellationToken = default) =>
        hub.Clients.All.SendAsync(ProductionOrderChanged, order, cancellationToken);
}
