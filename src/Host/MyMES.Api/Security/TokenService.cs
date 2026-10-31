using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MyMES.Abstractions.Security;
using MyMES.Abstractions.Time;
using MyMES.Contracts.Identity;
using MyMES.Identity;

namespace MyMES.Api.Security;

/// <summary>Issues the bearer tokens clients use to call the API.</summary>
public interface ITokenService
{
    Task<LoginResponse> IssueAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ITokenService"/>
public sealed class TokenService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IOptions<JwtOptions> options,
    IDateTimeProvider clock,
    SigningCredentialsProvider credentials) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    /// <summary>
    /// Builds a token carrying the user's roles and the permissions those roles grant.
    /// </summary>
    /// <remarks>
    /// Permissions are flattened into the token so the API authorises from the token alone,
    /// without a database round trip per request. The cost is latency on a permission change: the
    /// user keeps the old rights until their token expires. With a one-hour lifetime that is the
    /// right trade for a shop-floor system, and revocation is a sign-out.
    /// </remarks>
    public async Task<LoginResponse> IssueAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();

        var roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var roleName in roles)
        {
            var role = await roleManager.FindByNameAsync(roleName).ConfigureAwait(false);
            if (role is null)
            {
                continue;
            }

            foreach (var claim in await roleManager.GetClaimsAsync(role).ConfigureAwait(false))
            {
                if (claim.Type == MesPermissions.ClaimType)
                {
                    permissions.Add(claim.Value);
                }
            }
        }

        var now = clock.UtcNow;
        var expires = now.AddMinutes(_options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Id)
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            claims.Add(new Claim("display_name", user.DisplayName));
        }

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(p => new Claim(MesPermissions.ClaimType, p)));

        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(credentials.Key, SecurityAlgorithms.HmacSha256));

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);

        return new LoginResponse(
            encoded,
            expires,
            new UserInfoDto(user.Id, user.UserName ?? user.Id, user.Email, user.DisplayName, [.. roles]));
    }
}

/// <summary>
/// Holds the signing key as a single instance.
/// </summary>
/// <remarks>
/// Registered as a singleton so that the development fallback generates <em>one</em> key per
/// process. Resolving it per request would mint a new key each time and no token would validate.
/// </remarks>
public sealed class SigningCredentialsProvider
{
    public SigningCredentialsProvider(SymmetricSecurityKey key) => Key = key;

    public SymmetricSecurityKey Key { get; }

    public static SigningCredentialsProvider Create(string key) =>
        new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)));
}
