using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MyMES.Contracts.Machines;
using MyMES.Contracts.Production;

namespace MyMES.Messaging;

/// <summary>
/// Pushes changes to connected clients.
/// </summary>
/// <remarks>
/// Transport-agnostic on purpose: this project knows nothing about SignalR. The API supplies a
/// SignalR-backed implementation, and anything else (a wall display over MQTT, a test double)
/// plugs in the same way.
/// </remarks>
public interface IRealtimeNotifier
{
    Task MachineChangedAsync(MachineDto machine, CancellationToken cancellationToken = default);

    Task ProductionOrderChangedAsync(ProductionOrderDto order, CancellationToken cancellationToken = default);

    Task TelemetryAsync(MachineTelemetryReceived telemetry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Does nothing. Registered so that code depending on the notifier works in hosts without a
/// real-time transport, such as the worker service and the test suite.
/// </summary>
public sealed class NullRealtimeNotifier(ILogger<NullRealtimeNotifier> logger) : IRealtimeNotifier
{
    public Task MachineChangedAsync(MachineDto machine, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("No real-time transport; dropped machine update for {MachineCode}.", machine.MachineCode);
        return Task.CompletedTask;
    }

    public Task ProductionOrderChangedAsync(ProductionOrderDto order, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("No real-time transport; dropped order update for {OrderNumber}.", order.OrderNumber);
        return Task.CompletedTask;
    }

    public Task TelemetryAsync(MachineTelemetryReceived telemetry, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("No real-time transport; dropped telemetry for {MachineCode}.", telemetry.MachineCode);
        return Task.CompletedTask;
    }
}

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the no-op notifier as the default. A host with a real transport replaces it,
    /// which is exactly the "replace a default implementation" seam extensions use.
    /// </summary>
    public static IServiceCollection AddMessagingDefaults(this IServiceCollection services)
    {
        services.TryAddScoped<IRealtimeNotifier, NullRealtimeNotifier>();
        return services;
    }
}
