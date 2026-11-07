using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMES.Abstractions.Security;

namespace MyMES.Identity;

/// <summary>
/// Credentials for the very first administrator.
/// </summary>
/// <remarks>
/// Supplied through configuration, never through source. In development use user-secrets; in a
/// container pass environment variables. If nothing is configured no account is created and the
/// seeder says so in the log, which is safer than shipping a known default password.
/// </remarks>
public sealed class IdentityBootstrapOptions
{
    public const string SectionName = "Identity:Bootstrap";

    public string? AdministratorUserName { get; set; }

    public string? AdministratorEmail { get; set; }

    public string? AdministratorPassword { get; set; }

    /// <summary>True when all three values are present.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AdministratorUserName)
        && !string.IsNullOrWhiteSpace(AdministratorEmail)
        && !string.IsNullOrWhiteSpace(AdministratorPassword);
}

/// <summary>Creates the roles, their permission claims and, once, the first administrator.</summary>
public interface IIdentitySeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IIdentitySeeder"/>
public sealed class IdentitySeeder(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IPermissionCatalog permissions,
    IOptions<IdentityBootstrapOptions> bootstrapOptions,
    ILogger<IdentitySeeder> logger) : IIdentitySeeder
{
    private readonly IdentityBootstrapOptions _bootstrap = bootstrapOptions.Value;

    /// <summary>
    /// Runs on every start-up and is idempotent: it adds what is missing and leaves the rest
    /// alone. Nothing here deletes or rewrites existing data.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(cancellationToken).ConfigureAwait(false);
        await SeedFirstAdministratorAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var (name, description) in MesRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var role = await roleManager.FindByNameAsync(name).ConfigureAwait(false);
            if (role is null)
            {
                role = new ApplicationRole(name, description);
                var created = await roleManager.CreateAsync(role).ConfigureAwait(false);
                if (!created.Succeeded)
                {
                    logger.LogError("Could not create role {Role}: {Errors}", name, Describe(created));
                    continue;
                }

                logger.LogInformation("Role {Role} created.", name);
            }

            await SyncRolePermissionsAsync(role, name).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Adds any permission claim the role is missing. Claims that are present are left as they
    /// are, so a permission an administrator removed by hand does not silently come back.
    /// </summary>
    private async Task SyncRolePermissionsAsync(ApplicationRole role, string roleName)
    {
        var existing = await roleManager.GetClaimsAsync(role).ConfigureAwait(false);
        var held = existing
            .Where(c => c.Type == MesPermissions.ClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var permission in permissions.ForRole(roleName))
        {
            if (held.Contains(permission))
            {
                continue;
            }

            var result = await roleManager
                .AddClaimAsync(role, new Claim(MesPermissions.ClaimType, permission))
                .ConfigureAwait(false);

            if (!result.Succeeded)
            {
                logger.LogError(
                    "Could not grant {Permission} to {Role}: {Errors}",
                    permission,
                    roleName,
                    Describe(result));
            }
        }
    }

    private async Task SeedFirstAdministratorAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var administrators = await userManager.GetUsersInRoleAsync(MesRoles.Administrator).ConfigureAwait(false);
        if (administrators.Count > 0)
        {
            return;
        }

        if (!_bootstrap.IsConfigured)
        {
            logger.LogWarning(
                "No administrator exists and {Section} is not configured, so none was created. "
                + "Set {Section}:AdministratorUserName, :AdministratorEmail and :AdministratorPassword "
                + "and restart to create the first account.",
                IdentityBootstrapOptions.SectionName,
                IdentityBootstrapOptions.SectionName);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = _bootstrap.AdministratorUserName,
            Email = _bootstrap.AdministratorEmail,
            EmailConfirmed = true,
            DisplayName = _bootstrap.AdministratorUserName,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await userManager.CreateAsync(user, _bootstrap.AdministratorPassword!).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            logger.LogError("Could not create the first administrator: {Errors}", Describe(created));
            return;
        }

        var assigned = await userManager.AddToRoleAsync(user, MesRoles.Administrator).ConfigureAwait(false);
        if (!assigned.Succeeded)
        {
            logger.LogError("Could not assign the Administrator role: {Errors}", Describe(assigned));
            return;
        }

        logger.LogInformation(
            "First administrator {UserName} created. Change this password after signing in.",
            user.UserName);
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
}
