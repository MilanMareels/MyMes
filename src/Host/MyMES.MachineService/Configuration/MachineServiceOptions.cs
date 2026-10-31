namespace MyMES.MachineService.Configuration;

/// <summary>How the worker polls, what it polls, and how it reaches the API.</summary>
public sealed class MachineServiceOptions
{
    public const string SectionName = "MachineService";

    /// <summary>Seconds between polling rounds.</summary>
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Machines this instance is responsible for. Defaults to empty: the real list comes from
    /// configuration (<c>MachineService:MachineCodes</c>). A non-empty initializer here would be
    /// appended to by the configuration binder, duplicating whatever appsettings.json declares.
    /// </summary>
    public IList<string> MachineCodes { get; set; } = [];

    /// <summary>Base address of the MES API.</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5100";

    /// <summary>
    /// Service account the worker signs in with.
    /// </summary>
    /// <remarks>
    /// Without credentials the worker still polls and logs, but publishes nothing. That is
    /// deliberate: it has to be startable for a developer who has not set up an account yet,
    /// and silently writing to the MES without an identity would be worse than not writing.
    /// </remarks>
    public string? UserName { get; set; }

    public string? Password { get; set; }

    public SimulationOptions Simulation { get; set; } = new();

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);
}

/// <summary>Probabilities that shape the simulated machines, per poll.</summary>
public sealed class SimulationOptions
{
    /// <summary>Chance an idle machine starts producing.</summary>
    public double StartChance { get; set; } = 0.25;

    /// <summary>Chance a running machine stops normally.</summary>
    public double StopChance { get; set; } = 0.10;

    /// <summary>Chance a running machine faults.</summary>
    public double FaultChance { get; set; } = 0.04;

    /// <summary>Chance a faulted machine is repaired.</summary>
    public double RepairChance { get; set; } = 0.30;

    /// <summary>Chance a machine cannot be reached at all.</summary>
    public double UnreachableChance { get; set; } = 0.03;
}
