using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using MyMES.Api.Security;
using MyMES.Contracts.Identity;
using MyMES.Identity;

namespace MyMES.Api.Endpoints;

/// <summary>Sign-in and "who am I" endpoints.</summary>
internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth").WithTags("Authentication");

        group.MapPost("/login", async (
                LoginRequest request,
                SignInManager<ApplicationUser> signInManager,
                UserManager<ApplicationUser> userManager,
                ITokenService tokens,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                var logger = loggerFactory.CreateLogger("MyMES.Api.Auth");
                var user = await userManager.FindByNameAsync(request.UserName);

                // One message for every failure mode: telling an attacker which half was wrong
                // turns the login form into a user-name oracle.
                if (user is null || !user.IsActive)
                {
                    logger.LogWarning("Failed sign-in for {UserName}: unknown or inactive.", request.UserName);
                    return Results.Problem(
                        title: "Sign-in failed",
                        detail: "The user name or password is incorrect.",
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                // lockoutOnFailure keeps Identity's brute-force counter working.
                var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

                if (result.IsLockedOut)
                {
                    logger.LogWarning("Sign-in for {UserName} rejected: locked out.", request.UserName);
                    return Results.Problem(
                        title: "Account locked",
                        detail: "Too many failed attempts. Try again later.",
                        statusCode: StatusCodes.Status423Locked);
                }

                if (!result.Succeeded)
                {
                    logger.LogWarning("Failed sign-in for {UserName}: bad password.", request.UserName);
                    return Results.Problem(
                        title: "Sign-in failed",
                        detail: "The user name or password is incorrect.",
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                logger.LogInformation("User {UserName} signed in.", user.UserName);
                return Results.Ok(await tokens.IssueAsync(user, cancellationToken));
            })
            .WithName("Login")
            .WithSummary("Exchanges credentials for a bearer token.")
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked)
            .AllowAnonymous();

        group.MapGet("/me", (ClaimsPrincipal principal) =>
            {
                var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                if (id is null)
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new UserInfoDto(
                    id,
                    principal.FindFirstValue(ClaimTypes.Name) ?? id,
                    principal.FindFirstValue(ClaimTypes.Email),
                    principal.FindFirstValue("display_name"),
                    [.. principal.FindAll(ClaimTypes.Role).Select(c => c.Value)]));
            })
            .WithName("GetCurrentUser")
            .WithSummary("Returns the caller's identity and roles.")
            .Produces<UserInfoDto>()
            .RequireAuthorization();

        return endpoints;
    }
}
