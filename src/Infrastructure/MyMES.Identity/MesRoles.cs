using MyMES.Abstractions.Security;

namespace MyMES.Identity;

/// <summary>
/// The roles shipped with the platform.
/// </summary>
/// <remarks>
/// Roles are coarse job functions and are deliberately few. Fine-grained rights are the
/// <see cref="MesPermissions"/> constants, which is what lets a module or plugin introduce a new
/// right without anyone having to invent another role.
/// </remarks>
public static class MesRoles
{
    public const string Administrator = "Administrator";
    public const string Supervisor = "Supervisor";
    public const string Operator = "Operator";
    public const string Maintenance = "Maintenance";
    public const string Quality = "Quality";
    public const string Planner = "Planner";

    /// <summary>Every built-in role with the description shown in the administration screen.</summary>
    public static IReadOnlyList<(string Name, string Description)> All { get; } =
    [
        (Administrator, "Full access, including user administration."),
        (Supervisor, "Oversees production; may override order and machine state."),
        (Operator, "Runs jobs on the floor and books production."),
        (Maintenance, "Maintains machines and manages machine status."),
        (Quality, "Performs inspections and manages quality records."),
        (Planner, "Creates and schedules production orders.")
    ];

    public static IReadOnlyList<string> Names { get; } = [.. All.Select(r => r.Name)];

    /// <summary>
    /// Which permissions each built-in role carries out of the box.
    /// </summary>
    /// <remarks>
    /// This table is the platform's default grant. Modules and plugins extend it at start-up
    /// through <see cref="IPermissionCatalog"/> rather than by editing it here.
    /// </remarks>
    public static IReadOnlyDictionary<string, string[]> DefaultGrants { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [Administrator] =
            [
                MesPermissions.MachinesRead, MesPermissions.MachinesWrite, MesPermissions.MachinesChangeStatus,
                MesPermissions.ProductionRead, MesPermissions.ProductionWrite, MesPermissions.ProductionExecute,
                MesPermissions.ProductsRead, MesPermissions.ProductsWrite, MesPermissions.ProductsRelease,
                MesPermissions.QualityRead, MesPermissions.QualityWrite,
                MesPermissions.InventoryRead,
                MesPermissions.UsersRead, MesPermissions.UsersWrite
            ],
            [Supervisor] =
            [
                MesPermissions.MachinesRead, MesPermissions.MachinesChangeStatus,
                MesPermissions.ProductionRead, MesPermissions.ProductionWrite, MesPermissions.ProductionExecute,
                MesPermissions.ProductsRead,
                MesPermissions.QualityRead,
                MesPermissions.InventoryRead,
                MesPermissions.UsersRead
            ],
            [Operator] =
            [
                MesPermissions.MachinesRead, MesPermissions.MachinesChangeStatus,
                MesPermissions.ProductionRead, MesPermissions.ProductionExecute,
                MesPermissions.ProductsRead,
                MesPermissions.InventoryRead
            ],
            [Maintenance] =
            [
                MesPermissions.MachinesRead, MesPermissions.MachinesWrite, MesPermissions.MachinesChangeStatus,
                MesPermissions.ProductionRead
            ],
            [Quality] =
            [
                MesPermissions.MachinesRead,
                MesPermissions.ProductionRead,
                MesPermissions.ProductsRead,
                MesPermissions.QualityRead, MesPermissions.QualityWrite
            ],
            [Planner] =
            [
                MesPermissions.MachinesRead,
                MesPermissions.ProductionRead, MesPermissions.ProductionWrite,
                MesPermissions.ProductsRead, MesPermissions.ProductsWrite, MesPermissions.ProductsRelease,
                MesPermissions.InventoryRead
            ]
        };
}
