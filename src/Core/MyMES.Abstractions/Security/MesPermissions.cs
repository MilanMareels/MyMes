namespace MyMES.Abstractions.Security;

/// <summary>
/// The rights the platform knows about, issued as claims of type <see cref="ClaimType"/>.
/// </summary>
/// <remarks>
/// <para>
/// These are plain constants and live in Abstractions on purpose: an endpoint in a module says
/// <c>RequireAuthorization(MesPermissions.MachinesWrite)</c> without the module having to
/// reference the Identity infrastructure. Which roles hold which permission is an identity
/// concern and is configured there.
/// </para>
/// <para>
/// Endpoints authorise against a permission, never against a role. That indirection is what lets
/// an installation hand an existing right to a different role, or a module introduce a new right,
/// without touching endpoint code.
/// </para>
/// </remarks>
public static class MesPermissions
{
    /// <summary>Claim type under which permissions are issued.</summary>
    public const string ClaimType = "mymes.permission";

    public const string MachinesRead = "machines.read";
    public const string MachinesWrite = "machines.write";
    public const string MachinesChangeStatus = "machines.change_status";

    public const string ProductionRead = "production.read";
    public const string ProductionWrite = "production.write";
    public const string ProductionExecute = "production.execute";

    public const string ProductsRead = "products.read";
    public const string ProductsWrite = "products.write";
    public const string ProductsRelease = "products.release";

    public const string QualityRead = "quality.read";
    public const string QualityWrite = "quality.write";

    public const string InventoryRead = "inventory.read";

    public const string UsersRead = "users.read";
    public const string UsersWrite = "users.write";
}
