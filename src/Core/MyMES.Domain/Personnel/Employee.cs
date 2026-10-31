using MyMES.Domain.Common;

namespace MyMES.Domain.Personnel;

/// <summary>
/// A person working in the plant.
/// </summary>
/// <remarks>
/// Deliberately separate from the Identity user. Not every employee has a login (contractors,
/// shared terminals), and sign-in concerns do not belong in the domain. The link is a nullable
/// <see cref="IdentityUserId"/> owned by this side, so the domain stays free of Identity types.
/// </remarks>
public sealed class Employee : AuditableEntity
{
    private Employee()
    {
    }

    private Employee(Guid id, string employeeNumber, string firstName, string lastName) : base(id)
    {
        EmployeeNumber = employeeNumber;
        FirstName = firstName;
        LastName = lastName;
        IsActive = true;
    }

    /// <summary>Unique badge or personnel number.</summary>
    public string EmployeeNumber { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    /// <summary>Identity user this employee signs in with, when they have an account.</summary>
    public string? IdentityUserId { get; private set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public static Employee Create(string employeeNumber, string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber))
        {
            throw new DomainException("employee.number_required", "An employee needs a number.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new DomainException("employee.name_required", "An employee needs a last name.");
        }

        return new Employee(
            NewId(),
            employeeNumber.Trim().ToUpperInvariant(),
            firstName.Trim(),
            lastName.Trim());
    }

    public void UpdateName(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new DomainException("employee.name_required", "An employee needs a last name.");
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }

    /// <summary>Links this employee to an Identity account so their bookings carry a real name.</summary>
    public void LinkIdentityUser(string identityUserId)
    {
        if (string.IsNullOrWhiteSpace(identityUserId))
        {
            throw new DomainException("employee.identity_required", "An identity user id is required.");
        }

        IdentityUserId = identityUserId;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
