using System.ComponentModel.DataAnnotations;

namespace MyMES.Contracts.Identity;

/// <summary>Credentials posted to the token endpoint.</summary>
public sealed record LoginRequest
{
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// A successful sign-in. The token is a bearer JWT the caller sends on subsequent requests.
/// </summary>
public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserInfoDto User);

/// <summary>Who the caller is and what they are allowed to do.</summary>
public sealed record UserInfoDto(
    string Id,
    string UserName,
    string? Email,
    string? DisplayName,
    IReadOnlyList<string> Roles);

/// <summary>A user account as shown in the administration screen.</summary>
public sealed record UserDto(
    string Id,
    string UserName,
    string? Email,
    string? DisplayName,
    string? EmployeeNumber,
    bool IsActive,
    bool IsLockedOut,
    IReadOnlyList<string> Roles,
    DateTime CreatedAtUtc);

/// <summary>Creates a user account and optionally its employee profile.</summary>
public sealed record CreateUserRequest
{
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = string.Empty;

    [StringLength(128)]
    public string? FirstName { get; set; }

    [StringLength(128)]
    public string? LastName { get; set; }

    [StringLength(32)]
    public string? EmployeeNumber { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = [];
}

/// <summary>Replaces the role membership of a user.</summary>
public sealed record UpdateUserRolesRequest
{
    public IReadOnlyList<string> Roles { get; set; } = [];
}

