using System.Security.Claims;
using MyMES.Abstractions.Security;

namespace MyMES.Api.Security;

/// <summary>
/// The current user, read from the request's principal.
/// </summary>
/// <remarks>
/// Registered by the API in place of the system default, which is what lets the persistence layer
/// stamp audit columns with a real name without ever knowing about HTTP.
/// </remarks>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName =>
        Principal?.FindFirstValue("display_name")
        ?? Principal?.FindFirstValue(ClaimTypes.Name);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
