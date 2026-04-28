using BillFolder.Application.Dtos.Cycles;
using FluentValidation;

namespace BillFolder.Application.Validators.Cycles;

public class CreateCycleRequestValidator : AbstractValidator<CreateCycleRequest>
{
    public CreateCycleRequestValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEqual(default(DateOnly))
            .WithMessage("Data de início é obrigatória.");

        RuleFor(x => x.EndDate)
            .NotEqual(default(DateOnly))
            .WithMessage("Data de fim é obrigatória.");

        // Cross-field: start < end (só roda se ambas foram preenchidas)
        RuleFor(x => x)
            .Must(x => x.StartDate < x.EndDate)
            .WithMessage("Data de início deve ser anterior à data de fim.")
            .When(x => x.StartDate != default && x.EndDate != default);

        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("Label é obrigatório.")
            .MaximumLength(100);
    }
}

public class UpdateCycleRequestValidator : AbstractValidator<UpdateCycleRequest>
{
    public UpdateCycleRequestValidator()
    {
        RuleFor(x => x.StartDate!.Value)
            .NotEqual(default(DateOnly))
            .When(x => x.StartDate.HasValue);

        RuleFor(x => x.EndDate!.Value)
            .NotEqual(default(DateOnly))
            .When(x => x.EndDate.HasValue);

        RuleFor(x => x.Label!)
            .NotEmpty().MaximumLength(100)
            .When(x => x.Label is not null);

        // Cross-field: se ambas presentes, start < end
        RuleFor(x => x)
            .Must(x => x.StartDate!.Value < x.EndDate!.Value)
            .WithMessage("Data de início deve ser anterior à data de fim.")
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue);
    }
}
