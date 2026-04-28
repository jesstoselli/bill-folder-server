using BillFolder.Application.Dtos.Incomes;
using FluentValidation;

namespace BillFolder.Application.Validators.Incomes;

public class CreateIncomeEntryRequestValidator : AbstractValidator<CreateIncomeEntryRequest>
{
    public CreateIncomeEntryRequestValidator()
    {
        RuleFor(x => x.ExpectedAmount)
            .GreaterThan(0)
            .WithMessage("Valor esperado deve ser maior que zero.");

        RuleFor(x => x.ExpectedDate)
            .NotEqual(default(DateOnly))
            .WithMessage("Data esperada é obrigatória.");

        RuleFor(x => x.SourceId!.Value)
            .NotEqual(Guid.Empty)
            .When(x => x.SourceId.HasValue);

        RuleFor(x => x.Notes!)
            .MaximumLength(500)
            .When(x => x.Notes is not null);
    }
}

public class UpdateIncomeEntryRequestValidator : AbstractValidator<UpdateIncomeEntryRequest>
{
    public UpdateIncomeEntryRequestValidator()
    {
        RuleFor(x => x.ExpectedAmount!.Value)
            .GreaterThan(0)
            .When(x => x.ExpectedAmount.HasValue)
            .WithMessage("Valor esperado deve ser maior que zero.");

        RuleFor(x => x.ActualAmount!.Value)
            .GreaterThan(0)
            .When(x => x.ActualAmount.HasValue)
            .WithMessage("Valor recebido deve ser maior que zero.");

        RuleFor(x => x.ExpectedDate!.Value)
            .NotEqual(default(DateOnly))
            .When(x => x.ExpectedDate.HasValue);

        RuleFor(x => x.SourceId!.Value)
            .NotEqual(Guid.Empty)
            .When(x => x.SourceId.HasValue);

        RuleFor(x => x.Notes!)
            .MaximumLength(500)
            .When(x => x.Notes is not null);
    }
}
