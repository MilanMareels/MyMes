using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyMES.Abstractions.Security;
using MyMES.Abstractions.Time;
using MyMES.Identity;
using MyMES.Messaging;
using MyMES.Persistence;

namespace MyMES.Infrastructure;

/// <summary>The system clock. The only place <see cref="DateTime.UtcNow"/> is read.</summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// The "no user" principal, used by background services and the migration tooling.
/// </summary>
public sealed class SystemCurrentUser : ICurrentUser
{
    public string? UserId => null;

    public string? UserName => "system";

    public bool IsAuthenticated => false;

    public bool IsInRole(string role) => false;
}

public static class InfrastructureDependencyInjection
{
    /// <summary>
    /// Registers the technical services every host needs: clock, current user, database,
    /// identity building blocks and the messaging defaults.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.TryAddScoped<ICurrentUser, SystemCurrentUser>();

        services.AddPersistence(configuration);

        services.Configure<IdentityBootstrapOptions>(
            configuration.GetSection(IdentityBootstrapOptions.SectionName));
        services.TryAddScoped<IIdentitySeeder, IdentitySeeder>();

        services.AddMessagingDefaults();

        return services;
    }
}
