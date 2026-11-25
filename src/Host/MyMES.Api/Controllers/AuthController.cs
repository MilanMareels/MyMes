using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyMES.Api.Security;
using MyMES.Application.Identity;
using MyMES.Infrastructure;

namespace MyMES.Api.Controllers;

/// <summary>Sign-in and "who am I".</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/auth")]
public sealed class AuthController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ITokenService tokens,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByNameAsync(request.UserName);

        // One message for every failure mode: telling an attacker which half was wrong
        // turns the login form into a user-name oracle.
        if (user is null || !user.IsActive)
        {
            logger.LogWarning("Failed sign-in for {UserName}: unknown or inactive.", request.UserName);
            return Problem(
                title: "Sign-in failed",
                detail: "The user name or password is incorrect.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // lockoutOnFailure keeps Identity's brute-force counter working.
        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            logger.LogWarning("Sign-in for {UserName} rejected: locked out.", request.UserName);
            return Problem(
                title: "Account locked",
                detail: "Too many failed attempts. Try again later.",
                statusCode: StatusCodes.Status423Locked);
        }

        if (!result.Succeeded)
        {
            logger.LogWarning("Failed sign-in for {UserName}: bad password.", request.UserName);
            return Problem(
                title: "Sign-in failed",
                detail: "The user name or password is incorrect.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        logger.LogInformation("User {UserName} signed in.", user.UserName);
        return Ok(await tokens.IssueAsync(user, cancellationToken));
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id is null)
        {
            return Unauthorized();
        }

        return Ok(new UserInfoDto(
            id,
            User.FindFirstValue(ClaimTypes.Name) ?? id,
            User.FindFirstValue(ClaimTypes.Email),
            User.FindFirstValue("display_name"),
            [.. User.FindAll(ClaimTypes.Role).Select(c => c.Value)]));
    }
}
