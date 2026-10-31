using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMES.Contracts.Machines;
using MyMES.MachineService.Configuration;

namespace MyMES.MachineService.Drivers;

/// <summary>
/// Stands in for real equipment until the protocol drivers exist.
/// </summary>
/// <remarks>
/// Deliberately imperfect: it drifts between states, produces plausible signal values and
/// occasionally fails to answer. A simulator that always succeeds would let error handling rot
/// unnoticed, and the unreachable path is the one that matters most on a shop floor.
/// </remarks>
public sealed class SimulatedMachineDriver : IMachineDriver
{
    private readonly SimulationOptions _options;
    private readonly ILogger<SimulatedMachineDriver> _logger;
    private readonly Dictionary<string, MachineStatusDto> _states;
    private readonly Random _random = new();
    private readonly Lock _gate = new();

    public SimulatedMachineDriver(
        IOptions<MachineServiceOptions> options,
        ILogger<SimulatedMachineDriver> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value.Simulation;
        _logger = logger;
        MachineCodes = [.. options.Value.MachineCodes];
        _states = MachineCodes.ToDictionary(code => code, _ => MachineStatusDto.Idle, StringComparer.Ordinal);
    }

    public string Protocol => "simulated";

    public IReadOnlyCollection<string> MachineCodes { get; }

    public Task<MachineReading?> ReadAsync(string machineCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        MachineStatusDto status;
        lock (_gate)
        {
            if (!_states.TryGetValue(machineCode, out var current))
            {
                _logger.LogWarning("{MachineCode} is not handled by the {Protocol} driver.", machineCode, Protocol);
                return Task.FromResult<MachineReading?>(null);
            }

            // An unreachable machine is a normal outcome, not an exception.
            if (_random.NextDouble() < _options.UnreachableChance)
            {
                return Task.FromResult<MachineReading?>(null);
            }

            status = Advance(current);
            _states[machineCode] = status;
        }

        var signals = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["spindle_temperature_c"] = Math.Round(35 + (_random.NextDouble() * 45), 1),
            ["cycle_count"] = _random.Next(0, 4),
            ["feed_rate_mm_min"] = status == MachineStatusDto.Running
                ? Math.Round(800 + (_random.NextDouble() * 400), 0)
                : 0
        };

        return Task.FromResult<MachineReading?>(
            new MachineReading(machineCode, status, signals, DateTime.UtcNow));
    }

    /// <summary>
    /// Moves a machine along a plausible state path: idle starts, running occasionally faults,
    /// a fault clears to idle rather than straight back to running.
    /// </summary>
    private MachineStatusDto Advance(MachineStatusDto current)
    {
        var roll = _random.NextDouble();

        return current switch
        {
            MachineStatusDto.Idle when roll < _options.StartChance => MachineStatusDto.Running,
            MachineStatusDto.Running when roll < _options.FaultChance => MachineStatusDto.Faulted,
            MachineStatusDto.Running when roll < _options.FaultChance + _options.StopChance => MachineStatusDto.Idle,
            MachineStatusDto.Faulted when roll < _options.RepairChance => MachineStatusDto.Idle,
            _ => current
        };
    }
}
