using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MyMES.Abstractions.Security;

namespace MyMES.Identity;

/// <summary>
/// Turns every permission in the catalogue into a named authorization policy.
/// </summary>
/// <remarks>
/// Endpoints say <c>RequireAuthorization(MesPermissions.MachinesWrite)</c> and never name a role.
/// Because the policies are generated from the catalogue, a module that registers a new
/// permission gets a working policy without touching this file.
/// </remarks>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Registers one policy per permission, plus an <c>Administrator</c> convenience policy.
    /// Call after every module has had the chance to add its permissions to the catalogue.
    /// </summary>
    public static IServiceCollection AddMyMesAuthorization(
        this IServiceCollection services,
        IPermissionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        services.AddAuthorizationBuilder()
            // Anonymous access has to be opted into explicitly, per endpoint.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        services.AddAuthorization(options =>
        {
            foreach (var permission in catalog.All)
            {
                options.AddPolicy(permission, policy => policy
                    .RequireAuthenticatedUser()
                    .RequireClaim(MesPermissions.ClaimType, permission));
            }

            options.AddPolicy(MesRoles.Administrator, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(MesRoles.Administrator));
        });

        return services;
    }
}
