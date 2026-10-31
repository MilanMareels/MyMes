using MyMES.Contracts.Machines;

namespace MyMES.Messaging;

/// <summary>
/// Marks a message that crosses a process boundary.
/// </summary>
/// <remarks>
/// Domain events stay inside the application and may reference domain types. An integration event
/// is part of a contract between processes, so it carries only contract types and primitives, and
/// changing one is a breaking change for whoever is listening.
/// </remarks>
public interface IIntegrationEvent
{
    DateTime OccurredAtUtc { get; }
}

/// <summary>A machine reported its state, typically from the MachineService polling a device.</summary>
public sealed record MachineStateReported(
    string MachineCode,
    MachineStatusDto Status,
    DateTime OccurredAtUtc) : IIntegrationEvent;

/// <summary>A numeric reading from a device: temperature, cycle count, speed.</summary>
public sealed record MachineTelemetryReceived(
    string MachineCode,
    string Signal,
    double Value,
    string? Unit,
    DateTime OccurredAtUtc) : IIntegrationEvent;

/// <summary>A device could not be reached. Reported, never fatal.</summary>
public sealed record MachineCommunicationFailed(
    string MachineCode,
    string Reason,
    DateTime OccurredAtUtc) : IIntegrationEvent;
