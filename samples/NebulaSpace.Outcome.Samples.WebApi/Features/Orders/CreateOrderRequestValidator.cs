using FluentValidation;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Orders;

/// <summary>
/// Validates an incoming <see cref="CreateOrderRequest"/> with FluentValidation. When the
/// request is invalid, the validator throws a <see cref="ValidationException"/> that is
/// caught by the exception-handling middleware and mapped to a 400 Bad Request with the
/// field errors (via <c>options.Error.MapException</c> in Program.cs).
/// </summary>
public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.Product)
            .NotEmpty()
            .WithMessage("Nama produk wajib diisi.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Jumlah harus lebih dari 0.");
    }
}
