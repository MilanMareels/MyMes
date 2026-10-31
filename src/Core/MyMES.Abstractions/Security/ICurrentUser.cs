namespace MyMES.Abstractions.Security;

/// <summary>
/// The user behind the current operation, resolved by the host from the request context.
/// Background services get an unauthenticated instance rather than a null reference.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Identity key of the signed-in user, or <c>null</c> when there is none.</summary>
    string? UserId { get; }

    /// <summary>Display name used for audit stamps.</summary>
    string? UserName { get; }

    /// <summary><c>true</c> when the operation runs on behalf of a signed-in user.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Tests whether the current user holds the given role.</summary>
    bool IsInRole(string role);
}
