using BillFolder.Application.Dtos.CycleAdjustments;
using FluentValidation;

namespace BillFolder.Application.Validators.CycleAdjustments;

public class CreateCycleAdjustmentRequestValidator : AbstractValidator<CreateCycleAdjustmentRequest>
{
    public CreateCycleAdjustmentRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0)
            .WithMessage("Valor não pode ser negativo.");

        RuleFor(x => x.Date).NotEqual(default(DateOnly));
    }
}

public class UpdateCycleAdjustmentRequestValidator : AbstractValidator<UpdateCycleAdjustmentRequest>
{
    public UpdateCycleAdjustmentRequestValidator()
    {
        RuleFor(x => x.Label!).NotEmpty().MaximumLength(200)
            .When(x => x.Label is not null);

        RuleFor(x => x.Amount!.Value).GreaterThanOrEqualTo(0)
            .When(x => x.Amount.HasValue)
            .WithMessage("Valor não pode ser negativo.");

        RuleFor(x => x.Date!.Value).NotEqual(default(DateOnly))
            .When(x => x.Date.HasValue);
    }
}
