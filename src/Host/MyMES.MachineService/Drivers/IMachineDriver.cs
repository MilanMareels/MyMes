using MyMES.Contracts.Machines;

namespace MyMES.MachineService.Drivers;

/// <summary>One reading taken from a machine.</summary>
/// <param name="MachineCode">Machine the reading belongs to.</param>
/// <param name="Status">State the machine reports.</param>
/// <param name="Signals">Named numeric readings, for example temperature or cycle count.</param>
/// <param name="ReadAtUtc">When the reading was taken.</param>
public sealed record MachineReading(
    string MachineCode,
    MachineStatusDto Status,
    IReadOnlyDictionary<string, double> Signals,
    DateTime ReadAtUtc);

/// <summary>
/// Talks to one kind of machine.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam for everything the service will eventually speak: Modbus TCP, OPC UA, MQTT,
/// a serial protocol, or an ESP32 posting JSON. Each becomes a driver; nothing else in the
/// service changes, because the poller only knows this interface.
/// </para>
/// <para>
/// A driver must never throw for a machine being unreachable. That is an expected condition on a
/// shop floor and is reported through <see cref="ReadAsync"/> returning <c>null</c>, which the
/// poller turns into a communication-failure event.
/// </para>
/// </remarks>
public interface IMachineDriver
{
    /// <summary>Protocol this driver speaks, for diagnostics.</summary>
    string Protocol { get; }

    /// <summary>Machine codes this driver is responsible for.</summary>
    IReadOnlyCollection<string> MachineCodes { get; }

    /// <summary>
    /// Reads one machine. Returns <c>null</c> when the machine could not be reached.
    /// </summary>
    Task<MachineReading?> ReadAsync(string machineCode, CancellationToken cancellationToken = default);
}
