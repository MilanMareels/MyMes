using FluentValidation;
using MyMES.Contracts.Machines;
using MyMES.Contracts.Production;
using MyMES.Contracts.Products;

namespace MyMES.Application.Validation;

/// <summary>
/// Validation that data annotations cannot express.
/// </summary>
/// <remarks>
/// The split is deliberate. Shape rules (required, maximum length, numeric range) live as data
/// annotations on the contracts, because Swagger turns them into schema documentation and the
/// Blazor <c>EditForm</c> enforces them in the browser without a round trip. Rules that span
/// fields or need a format check live here and run on the server, where they cannot be bypassed.
/// </remarks>
public sealed class CreateMachineRequestValidator : AbstractValidator<CreateMachineRequest>
{
    public CreateMachineRequestValidator()
    {
        RuleFor(x => x.MachineCode)
            .Matches("^[A-Za-z0-9][A-Za-z0-9._-]*$")
            .WithMessage("Machine code may contain letters, digits, dot, dash and underscore only.");
    }
}

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.ProductCode)
            .Matches("^[A-Za-z0-9][A-Za-z0-9._-]*$")
            .WithMessage("Product code may contain letters, digits, dot, dash and underscore only.");
    }
}

public sealed class CreateProductionOrderRequestValidator : AbstractValidator<CreateProductionOrderRequest>
{
    public CreateProductionOrderRequestValidator()
    {
        RuleFor(x => x.OrderNumber)
            .Matches("^[A-Za-z0-9][A-Za-z0-9._-]*$")
            .WithMessage("Order number may contain letters, digits, dot, dash and underscore only.");

        RuleFor(x => x.PlannedEndUtc)
            .GreaterThanOrEqualTo(x => x.PlannedStartUtc)
            .WithMessage("Planned end cannot be before planned start.");
    }
}

public sealed class RescheduleProductionOrderRequestValidator : AbstractValidator<RescheduleProductionOrderRequest>
{
    public RescheduleProductionOrderRequestValidator()
    {
        RuleFor(x => x.PlannedEndUtc)
            .GreaterThanOrEqualTo(x => x.PlannedStartUtc)
            .WithMessage("Planned end cannot be before planned start.");
    }
}
