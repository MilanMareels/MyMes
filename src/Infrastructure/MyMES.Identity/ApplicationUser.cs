using Microsoft.AspNetCore.Identity;

namespace MyMES.Identity;

/// <summary>
/// A sign-in account.
/// </summary>
/// <remarks>
/// Kept deliberately thin. Everything about the <em>person</em> lives on the domain's
/// <c>Employee</c>; this type only carries what authentication itself needs.
/// </remarks>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>Name shown in the UI and written into audit stamps.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Deactivated accounts keep their history but cannot sign in. Separate from the Identity
    /// lockout, which is the automatic brute-force protection and expires on its own.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Links the account to its employee record, when one exists.</summary>
    public Guid? EmployeeId { get; set; }
}

/// <summary>A role, with a human-readable description for the administration screen.</summary>
public sealed class ApplicationRole : IdentityRole
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName, string description) : base(roleName) => Description = description;

    public string? Description { get; set; }
}
