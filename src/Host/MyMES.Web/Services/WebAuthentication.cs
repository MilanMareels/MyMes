using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MyMES.Abstractions.Security;
using MyMES.Contracts.Identity;

namespace MyMES.Web.Services;

/// <summary>Names shared by the cookie sign-in and the token handler.</summary>
public static class WebAuthenticationDefaults
{
    /// <summary>
    /// Claim holding the API bearer token.
    /// </summary>
    /// <remarks>
    /// The cookie is encrypted and signed by Data Protection, so the token is not readable by the
    /// browser or by script. Two consequences worth knowing: the cookie grows by the size of the
    /// token, and anyone who steals the cookie also holds the API token until it expires. For a
    /// higher bar, keep tokens server-side and put only a session key in the cookie.
    /// </remarks>
    public const string ApiTokenClaim = "mymes.api_token";

    public const string TokenExpiryClaim = "mymes.token_expires";
}

/// <summary>Signs a user in and out of the Blazor application's cookie session.</summary>
public static class WebSignIn
{
    /// <summary>
    /// Builds the principal from what the API returned and writes the authentication cookie.
    /// </summary>
    /// <remarks>
    /// The cookie's own lifetime is tied to the API token's, so the browser session cannot
    /// outlive the credential it carries.
    /// </remarks>
    public static Task SignInAsync(HttpContext httpContext, LoginResponse login)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(login);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, login.User.Id),
            new(ClaimTypes.Name, login.User.UserName),
            new(WebAuthenticationDefaults.ApiTokenClaim, login.AccessToken),
            new(WebAuthenticationDefaults.TokenExpiryClaim, login.ExpiresAtUtc.ToString("O"))
        };

        if (!string.IsNullOrWhiteSpace(login.User.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, login.User.Email));
        }

        if (!string.IsNullOrWhiteSpace(login.User.DisplayName))
        {
            claims.Add(new Claim("display_name", login.User.DisplayName));
        }

        claims.AddRange(login.User.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        return httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = login.ExpiresAtUtc
            });
    }

    public static Task SignOutAsync(HttpContext httpContext) =>
        httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
}

/// <summary>The signed-in user, for components that need more than <c>AuthorizeView</c> offers.</summary>
public sealed class WebCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public string? UserName =>
        Principal?.FindFirst("display_name")?.Value
        ?? Principal?.FindFirst(ClaimTypes.Name)?.Value;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
