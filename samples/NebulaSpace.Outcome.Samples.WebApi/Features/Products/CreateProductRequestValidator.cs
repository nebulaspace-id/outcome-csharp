using FluentValidation;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Products;

/// <summary>
/// Validates an incoming <see cref="CreateProductRequest"/>.
/// Thrown <see cref="ValidationException"/> is caught by the exception-handling
/// middleware and mapped to 400 Bad Request with field errors.
/// </summary>
public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(100).WithMessage("Product name must be at most 100 characters.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(50).WithMessage("Category must be at most 50 characters.");
    }
}
