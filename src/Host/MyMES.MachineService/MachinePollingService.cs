using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMES.Contracts.Machines;
using MyMES.MachineService.Api;
using MyMES.MachineService.Configuration;
using MyMES.MachineService.Drivers;

namespace MyMES.MachineService;

/// <summary>
/// Polls every configured machine and reports what changed to the MES.
/// </summary>
/// <remarks>
/// <para>
/// Only transitions are reported, not every reading: a machine that has been running for an hour
/// should not generate an audit entry every ten seconds.
/// </para>
/// <para>
/// Nothing in a polling round is allowed to end the loop. A driver that throws, an unreachable
/// machine or an API that is down all degrade to a log entry and a retry next round, because this
/// service running beside the MES must never be able to take production reporting down with it.
/// </para>
/// <para>
/// This service reads and reports. It issues no commands to equipment, and in particular has no
/// part in any safety function: emergency stop belongs to the machine's own safety chain, which
/// is independent of the MES by design and by regulation.
/// </para>
/// </remarks>
public sealed class MachinePollingService(
    IEnumerable<IMachineDriver> drivers,
    IMesApiGateway mes,
    MachineHealthState health,
    IOptions<MachineServiceOptions> options,
    ILogger<MachinePollingService> logger) : BackgroundService
{
    private readonly MachineServiceOptions _options = options.Value;
    private readonly Dictionary<string, MachineStatusDto> _lastReported = new(StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var driverList = drivers.ToList();

        logger.LogInformation(
            "MachineService polling {MachineCount} machine(s) every {Interval}s via {DriverCount} driver(s).",
            driverList.Sum(d => d.MachineCodes.Count),
            _options.PollIntervalSeconds,
            driverList.Count);

        if (!_options.HasCredentials)
        {
            logger.LogWarning(
                "No MachineService credentials configured. Machines are polled and logged but "
                + "nothing is reported to the MES. Set MachineService__UserName and __Password.");
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)));

        // One round immediately so a restart shows something without waiting for the first tick.
        await PollOnceAsync(driverList, stoppingToken).ConfigureAwait(false);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await PollOnceAsync(driverList, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task PollOnceAsync(List<IMachineDriver> driverList, CancellationToken cancellationToken)
    {
        foreach (var driver in driverList)
        {
            foreach (var machineCode in driver.MachineCodes)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await PollMachineAsync(driver, machineCode, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // A bad driver must not stop the other machines from being polled.
                    health.RecordFailure(machineCode);
                    logger.LogError(ex, "Polling {MachineCode} via {Protocol} failed.", machineCode, driver.Protocol);
                }
            }
        }
    }

    private async Task PollMachineAsync(IMachineDriver driver, string machineCode, CancellationToken cancellationToken)
    {
        var reading = await driver.ReadAsync(machineCode, cancellationToken).ConfigureAwait(false);

        if (reading is null)
        {
            health.RecordFailure(machineCode);
            logger.LogWarning("{MachineCode} could not be reached over {Protocol}.", machineCode, driver.Protocol);
            return;
        }

        health.RecordSuccess(machineCode);

        logger.LogDebug(
            "{MachineCode}: {Status}, signals {Signals}.",
            reading.MachineCode,
            reading.Status,
            string.Join(", ", reading.Signals.Select(s => $"{s.Key}={s.Value}")));

        if (_lastReported.TryGetValue(machineCode, out var previous) && previous == reading.Status)
        {
            return;
        }

        var reported = await mes
            .ReportStatusAsync(machineCode, reading.Status, cancellationToken)
            .ConfigureAwait(false);

        if (reported)
        {
            // Only remembered once the MES accepted it, so a failed push is retried next round.
            _lastReported[machineCode] = reading.Status;
            logger.LogInformation("{MachineCode} reported as {Status}.", machineCode, reading.Status);
        }
    }
}

/// <summary>
/// Tracks which machines last answered, so the worker's health endpoint says something useful.
/// </summary>
/// <remarks>
/// Reporting unhealthy because a machine is off would make an orchestrator restart a worker that
/// is doing its job. The worker is healthy while it is polling; unreachable machines are degraded
/// information, not a failed process.
/// </remarks>
public sealed class MachineHealthState
{
    private readonly Dictionary<string, DateTime> _lastSuccess = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> _lastFailure = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public void RecordSuccess(string machineCode)
    {
        lock (_gate)
        {
            _lastSuccess[machineCode] = DateTime.UtcNow;
        }
    }

    public void RecordFailure(string machineCode)
    {
        lock (_gate)
        {
            _lastFailure[machineCode] = DateTime.UtcNow;
        }
    }

    public IReadOnlyDictionary<string, object> Snapshot()
    {
        lock (_gate)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["machinesAnswering"] = _lastSuccess.Count,
                ["machinesFailing"] = _lastFailure.Count(f =>
                    !_lastSuccess.TryGetValue(f.Key, out var ok) || ok < f.Value)
            };
        }
    }
}

/// <summary>Reports the worker as healthy while the poll loop is alive.</summary>
public sealed class MachinePollingHealthCheck(MachineHealthState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(HealthCheckResult.Healthy("Polling.", state.Snapshot()));
}
