using MyMES.Domain.Common;

namespace MyMES.Domain.Machines;

/// <summary>Operational state of a machine as the MES understands it.</summary>
/// <remarks>
/// This mirrors what the shop floor reports. It is explicitly <em>not</em> a safety signal: the
/// MES never participates in emergency-stop logic, which stays in the machine's own safety chain.
/// </remarks>
public enum MachineStatus
{
    /// <summary>Known to the MES but not commissioned.</summary>
    Unknown = 0,

    /// <summary>Powered and available, no job running.</summary>
    Idle = 1,

    /// <summary>Producing.</summary>
    Running = 2,

    /// <summary>Stopped by a fault.</summary>
    Faulted = 3,

    /// <summary>Out of service for planned maintenance.</summary>
    Maintenance = 4,

    /// <summary>Permanently withdrawn; kept for historical traceability.</summary>
    Decommissioned = 5
}

/// <summary>
/// A production resource: a machine, cell or line that work can be executed on.
/// </summary>
public sealed class Machine : AuditableEntity
{
    private Machine()
    {
    }

    private Machine(Guid id, string machineCode, string name, string? description) : base(id)
    {
        MachineCode = machineCode;
        Name = name;
        Description = description;
        Status = MachineStatus.Idle;
    }

    /// <summary>Unique business key, for example <c>CNC-01</c>.</summary>
    public string MachineCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public MachineStatus Status { get; private set; }

    /// <summary>When the machine last entered <see cref="Status"/>; the basis for state duration.</summary>
    public DateTime StatusChangedAtUtc { get; private set; }

    /// <summary>Registers a new machine.</summary>
    /// <exception cref="DomainException">The code or name is blank.</exception>
    public static Machine Create(string machineCode, string name, string? description, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(machineCode))
        {
            throw new DomainException("machine.code_required", "A machine needs a code.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("machine.name_required", "A machine needs a name.");
        }

        var machine = new Machine(NewId(), machineCode.Trim().ToUpperInvariant(), name.Trim(), description?.Trim())
        {
            StatusChangedAtUtc = utcNow
        };

        machine.Raise(new MachineRegisteredEvent(machine.Id, machine.MachineCode, utcNow));
        return machine;
    }

    /// <summary>
    /// Moves the machine to a new status.
    /// </summary>
    /// <remarks>
    /// A decommissioned machine is terminal: it never returns to service, because its history must
    /// stay attached to the orders it produced. Re-registering is a deliberate, separate action.
    /// </remarks>
    /// <exception cref="DomainException">The machine has been decommissioned.</exception>
    public void ChangeStatus(MachineStatus status, DateTime utcNow)
    {
        if (Status == MachineStatus.Decommissioned)
        {
            throw new DomainException(
                "machine.decommissioned",
                $"Machine {MachineCode} is decommissioned and cannot change status.");
        }

        if (Status == status)
        {
            return;
        }

        var previous = Status;
        Status = status;
        StatusChangedAtUtc = utcNow;
        Raise(new MachineStatusChangedEvent(Id, MachineCode, previous, status, utcNow));
    }

    /// <summary>Updates the descriptive fields. The code is a business key and never changes.</summary>
    public void UpdateDetails(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("machine.name_required", "A machine needs a name.");
        }

        Name = name.Trim();
        Description = description?.Trim();
    }

    /// <summary>Withdraws the machine from service permanently.</summary>
    public void Decommission(DateTime utcNow)
    {
        if (Status == MachineStatus.Decommissioned)
        {
            return;
        }

        var previous = Status;
        Status = MachineStatus.Decommissioned;
        StatusChangedAtUtc = utcNow;
        Raise(new MachineStatusChangedEvent(Id, MachineCode, previous, Status, utcNow));
    }
}

/// <summary>Raised when a machine is first registered in the MES.</summary>
public sealed record MachineRegisteredEvent(Guid MachineId, string MachineCode, DateTime OccurredAtUtc) : IDomainEvent;

/// <summary>Raised whenever a machine moves between operational states.</summary>
public sealed record MachineStatusChangedEvent(
    Guid MachineId,
    string MachineCode,
    MachineStatus PreviousStatus,
    MachineStatus NewStatus,
    DateTime OccurredAtUtc) : IDomainEvent;
