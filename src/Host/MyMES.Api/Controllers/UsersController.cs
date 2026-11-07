using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMES.Abstractions.Security;
using MyMES.Application.Data;
using MyMES.Contracts.Identity;
using MyMES.Domain.Personnel;
using MyMES.Identity;

namespace MyMES.Api.Controllers;

/// <summary>User administration.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/users")]
public sealed class UsersController(
    UserManager<ApplicationUser> userManager,
    IRepository<Employee> employees) : ControllerBase
{
    [HttpGet]
    [Authorize(MesPermissions.UsersRead)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var users = await userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .Take(500)
            .ToListAsync(cancellationToken);

        var employeeNumbers = await employees.Query()
            .AsNoTracking()
            .Where(e => e.IdentityUserId != null)
            .ToDictionaryAsync(e => e.IdentityUserId!, e => e.EmployeeNumber, cancellationToken);

        var result = new List<UserDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new UserDto(
                user.Id,
                user.UserName ?? user.Id,
                user.Email,
                user.DisplayName,
                employeeNumbers.GetValueOrDefault(user.Id),
                user.IsActive,
                user.LockoutEnd > DateTimeOffset.UtcNow,
                [.. roles],
                user.CreatedAtUtc));
        }

        return Ok(result);
    }

    [HttpGet("roles")]
    [Authorize(MesPermissions.UsersRead)]
    public IActionResult Roles() => Ok(MesRoles.All.Select(r => new { r.Name, r.Description }));

    [HttpPost]
    [Authorize(MesPermissions.UsersWrite)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var unknownRoles = request.Roles.Where(r => !MesRoles.Names.Contains(r)).ToList();
        if (unknownRoles.Count > 0)
        {
            return Problem(
                title: "Invalid request",
                detail: $"Unknown role(s): {string.Join(", ", unknownRoles)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            EmailConfirmed = true,
            DisplayName = string.Join(" ", new[] { request.FirstName, request.LastName }
                .Where(p => !string.IsNullOrWhiteSpace(p))).Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (string.IsNullOrWhiteSpace(user.DisplayName))
        {
            user.DisplayName = request.UserName;
        }

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return Problem(
                title: "Invalid request",
                detail: string.Join(" ", created.Errors.Select(e => e.Description)),
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.Roles.Count > 0)
        {
            await userManager.AddToRolesAsync(user, request.Roles);
        }

        // An employee number turns the account into a person on the floor.
        if (!string.IsNullOrWhiteSpace(request.EmployeeNumber))
        {
            var employee = Employee.Create(
                request.EmployeeNumber,
                request.FirstName ?? string.Empty,
                request.LastName ?? request.UserName);

            employee.LinkIdentityUser(user.Id);
            employees.Add(employee);
            await employees.SaveChangesAsync(cancellationToken);

            user.EmployeeId = employee.Id;
            await userManager.UpdateAsync(user);
        }

        return Created($"/api/v1/users/{user.Id}", new UserDto(
            user.Id,
            user.UserName!,
            user.Email,
            user.DisplayName,
            request.EmployeeNumber,
            user.IsActive,
            IsLockedOut: false,
            [.. request.Roles],
            user.CreatedAtUtc));
    }

    [HttpPut("{id}/roles")]
    [Authorize(MesPermissions.UsersWrite)]
    public async Task<IActionResult> UpdateRoles(string id, UpdateUserRolesRequest request)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return Problem(
                title: "Not found",
                detail: $"No user with id {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var unknownRoles = request.Roles.Where(r => !MesRoles.Names.Contains(r)).ToList();
        if (unknownRoles.Count > 0)
        {
            return Problem(
                title: "Invalid request",
                detail: $"Unknown role(s): {string.Join(", ", unknownRoles)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var current = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, current.Except(request.Roles));
        await userManager.AddToRolesAsync(user, request.Roles.Except(current));

        return NoContent();
    }

    [HttpPost("{id}/deactivate")]
    [Authorize(MesPermissions.UsersWrite)]
    public async Task<IActionResult> Deactivate(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return Problem(
                title: "Not found",
                detail: $"No user with id {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        // Guard the caller against locking themselves out of the box.
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.Equals(currentUserId, id, StringComparison.Ordinal))
        {
            return Problem(
                title: "Invalid request",
                detail: "You cannot deactivate your own account.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (user.IsActive)
        {
            user.IsActive = false;
            await userManager.UpdateAsync(user);
        }

        return NoContent();
    }

    [HttpPost("{id}/activate")]
    [Authorize(MesPermissions.UsersWrite)]
    public async Task<IActionResult> Activate(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return Problem(
                title: "Not found",
                detail: $"No user with id {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (!user.IsActive)
        {
            user.IsActive = true;
            await userManager.UpdateAsync(user);
        }

        return NoContent();
    }
}
