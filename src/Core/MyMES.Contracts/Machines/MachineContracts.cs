using System.ComponentModel.DataAnnotations;

namespace MyMES.Contracts.Machines;

/// <summary>
/// Machine status as published on the wire.
/// </summary>
/// <remarks>
/// Mirrors the domain enum but is a separate type on purpose: the public contract must be able to
/// stay stable while the domain evolves. Mapping between the two is explicit and covered by tests.
/// </remarks>
public enum MachineStatusDto
{
    Unknown = 0,
    Idle = 1,
    Running = 2,
    Faulted = 3,
    Maintenance = 4,
    Decommissioned = 5
}

/// <summary>A machine as returned by the API.</summary>
public sealed record MachineDto(
    Guid Id,
    string MachineCode,
    string Name,
    string? Description,
    MachineStatusDto Status,
    DateTime StatusChangedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Registers a new machine.</summary>
public sealed record CreateMachineRequest
{
    [Required]
    [StringLength(32, MinimumLength = 1)]
    public string MachineCode { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(512)]
    public string? Description { get; set; }
}

/// <summary>Updates the descriptive fields of a machine. The code is immutable.</summary>
public sealed record UpdateMachineRequest
{
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(512)]
    public string? Description { get; set; }
}

/// <summary>Moves a machine to a new operational status.</summary>
public sealed record ChangeMachineStatusRequest
{
    [Required]
    public MachineStatusDto Status { get; set; }
}

