using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyMES.Abstractions.Security;
using MyMES.Application.Abstractions;
using MyMES.Contracts.Identity;
using MyMES.Domain.Personnel;
using MyMES.Identity;

namespace MyMES.Api.Endpoints;

/// <summary>User administration. Every endpoint needs an explicit user-management permission.</summary>
internal static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/users").WithTags("Users");

        group.MapGet("/", async (
                UserManager<ApplicationUser> userManager,
                IApplicationDbContext db,
                CancellationToken cancellationToken) =>
            {
                var users = await userManager.Users
                    .AsNoTracking()
                    .OrderBy(u => u.UserName)
                    .Take(500)
                    .ToListAsync(cancellationToken);

                var employeeNumbers = await db.Employees
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

                return Results.Ok(result);
            })
            .WithName("GetUsers")
            .WithSummary("Lists user accounts with their roles.")
            .Produces<List<UserDto>>()
            .RequireAuthorization(MesPermissions.UsersRead);

        group.MapGet("/roles", (CancellationToken cancellationToken) =>
                Results.Ok(MesRoles.All.Select(r => new { r.Name, r.Description })))
            .WithName("GetRoles")
            .WithSummary("Lists the roles available to assign.")
            .RequireAuthorization(MesPermissions.UsersRead);

        group.MapPost("/", async (
                CreateUserRequest request,
                UserManager<ApplicationUser> userManager,
                IApplicationDbContext db,
                CancellationToken cancellationToken) =>
            {
                var unknownRoles = request.Roles.Where(r => !MesRoles.Names.Contains(r)).ToList();
                if (unknownRoles.Count > 0)
                {
                    return Results.Problem(
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
                    // Identity's messages are safe to return: they describe the submitted
                    // password's shape, which the caller already knows.
                    return Results.Problem(
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
                    db.Employees.Add(employee);
                    await db.SaveChangesAsync(cancellationToken);

                    user.EmployeeId = employee.Id;
                    await userManager.UpdateAsync(user);
                }

                return Results.Created($"/api/v1/users/{user.Id}", new UserDto(
                    user.Id,
                    user.UserName!,
                    user.Email,
                    user.DisplayName,
                    request.EmployeeNumber,
                    user.IsActive,
                    IsLockedOut: false,
                    [.. request.Roles],
                    user.CreatedAtUtc));
            })
            .WithName("CreateUser")
            .WithSummary("Creates a user account and, optionally, its employee record.")
            .Produces<UserDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization(MesPermissions.UsersWrite);

        group.MapPut("/{id}/roles", async (
                string id,
                UpdateUserRolesRequest request,
                UserManager<ApplicationUser> userManager) =>
            {
                var user = await userManager.FindByIdAsync(id);
                if (user is null)
                {
                    return Results.Problem(
                        title: "Not found",
                        detail: $"No user with id {id}.",
                        statusCode: StatusCodes.Status404NotFound);
                }

                var unknownRoles = request.Roles.Where(r => !MesRoles.Names.Contains(r)).ToList();
                if (unknownRoles.Count > 0)
                {
                    return Results.Problem(
                        title: "Invalid request",
                        detail: $"Unknown role(s): {string.Join(", ", unknownRoles)}.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var current = await userManager.GetRolesAsync(user);
                await userManager.RemoveFromRolesAsync(user, current.Except(request.Roles));
                await userManager.AddToRolesAsync(user, request.Roles.Except(current));

                return Results.NoContent();
            })
            .WithName("UpdateUserRoles")
            .WithSummary("Replaces a user's role membership.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(MesPermissions.UsersWrite);

        group.MapPost("/{id}/deactivate", async (
                string id,
                UserManager<ApplicationUser> userManager,
                ClaimsPrincipal principal) =>
            {
                var user = await userManager.FindByIdAsync(id);
                if (user is null)
                {
                    return Results.Problem(
                        title: "Not found",
                        detail: $"No user with id {id}.",
                        statusCode: StatusCodes.Status404NotFound);
                }

                // Guard the caller against locking themselves out of the box.
                var currentUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.Equals(currentUserId, id, StringComparison.Ordinal))
                {
                    return Results.Problem(
                        title: "Invalid request",
                        detail: "You cannot deactivate your own account.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (user.IsActive)
                {
                    user.IsActive = false;
                    await userManager.UpdateAsync(user);
                }

                return Results.NoContent();
            })
            .WithName("DeactivateUser")
            .WithSummary("Deactivates a user account, preventing further sign-in.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization(MesPermissions.UsersWrite);

        group.MapPost("/{id}/activate", async (
                string id,
                UserManager<ApplicationUser> userManager) =>
            {
                var user = await userManager.FindByIdAsync(id);
                if (user is null)
                {
                    return Results.Problem(
                        title: "Not found",
                        detail: $"No user with id {id}.",
                        statusCode: StatusCodes.Status404NotFound);
                }

                if (!user.IsActive)
                {
                    user.IsActive = true;
                    await userManager.UpdateAsync(user);
                }

                return Results.NoContent();
            })
            .WithName("ActivateUser")
            .WithSummary("Reactivates a previously deactivated user account.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(MesPermissions.UsersWrite);

        return endpoints;
    }
}
