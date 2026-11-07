using System.Collections.Concurrent;
using MyMES.Abstractions.Security;

namespace MyMES.Identity;

/// <summary>
/// The set of permissions the running system knows about, including those contributed by modules
/// and plugins at start-up.
/// </summary>
/// <remarks>
/// Registered as a singleton and populated during service configuration, before the authorization
/// policies are built. An extension that introduces a right calls <see cref="Register"/> and the
/// host turns every registered permission into a policy automatically.
/// </remarks>
public interface IPermissionCatalog
{
    /// <summary>Adds a permission and optionally grants it to existing roles.</summary>
    /// <param name="permission">Permission name, for example <c>quality.approve</c>.</param>
    /// <param name="grantToRoles">Roles that should receive it when the database is seeded.</param>
    void Register(string permission, params string[] grantToRoles);

    /// <summary>Every known permission, platform and extension alike.</summary>
    IReadOnlyCollection<string> All { get; }

    /// <summary>The permissions a role carries.</summary>
    IReadOnlyCollection<string> ForRole(string role);
}

/// <inheritdoc cref="IPermissionCatalog"/>
public sealed class PermissionCatalog : IPermissionCatalog
{
    private readonly ConcurrentDictionary<string, byte> _permissions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, HashSet<string>> _roleGrants =
        new(StringComparer.Ordinal);

    public PermissionCatalog()
    {
        // Seed with the platform's own permissions; extensions add to this.
        foreach (var (role, permissions) in MesRoles.DefaultGrants)
        {
            var set = new HashSet<string>(permissions, StringComparer.Ordinal);
            _roleGrants[role] = set;

            foreach (var permission in permissions)
            {
                _permissions.TryAdd(permission, 0);
            }
        }
    }

    public IReadOnlyCollection<string> All => [.. _permissions.Keys.Order()];

    public void Register(string permission, params string[] grantToRoles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        _permissions.TryAdd(permission, 0);

        foreach (var role in grantToRoles ?? [])
        {
            var set = _roleGrants.GetOrAdd(role, static _ => new HashSet<string>(StringComparer.Ordinal));
            lock (set)
            {
                set.Add(permission);
            }
        }
    }

    public IReadOnlyCollection<string> ForRole(string role)
    {
        if (!_roleGrants.TryGetValue(role, out var set))
        {
            return [];
        }

        lock (set)
        {
            return [.. set];
        }
    }
}
