using BillFolder.Application.Dtos.Incomes;
using FluentValidation;

namespace BillFolder.Application.Validators.Incomes;

public class CreateIncomeSourceRequestValidator : AbstractValidator<CreateIncomeSourceRequest>
{
    public CreateIncomeSourceRequestValidator()
    {
        RuleFor(x => x.Origin)
            .NotEmpty().WithMessage("Origem é obrigatória.")
            .MaximumLength(100);

        RuleFor(x => x.DefaultAmount)
            .GreaterThan(0)
            .WithMessage("Valor padrão deve ser maior que zero.");

        RuleFor(x => x.ExpectedDay)
            .InclusiveBetween((short)1, (short)31)
            .WithMessage("Dia esperado deve estar entre 1 e 31.");

        RuleFor(x => x.StartDate)
            .NotEqual(default(DateOnly))
            .WithMessage("Data de início é obrigatória.");

        // end_date opcional, mas se fornecido deve ser >= start_date
        RuleFor(x => x)
            .Must(x => x.EndDate!.Value >= x.StartDate)
            .WithMessage("Data de fim deve ser igual ou posterior à data de início.")
            .When(x => x.EndDate.HasValue);
    }
}

public class UpdateIncomeSourceRequestValidator : AbstractValidator<UpdateIncomeSourceRequest>
{
    public UpdateIncomeSourceRequestValidator()
    {
        RuleFor(x => x.Origin!)
            .NotEmpty().MaximumLength(100)
            .When(x => x.Origin is not null);

        RuleFor(x => x.DefaultAmount!.Value)
            .GreaterThan(0)
            .When(x => x.DefaultAmount.HasValue)
            .WithMessage("Valor padrão deve ser maior que zero.");

        RuleFor(x => x.ExpectedDay!.Value)
            .InclusiveBetween((short)1, (short)31)
            .When(x => x.ExpectedDay.HasValue);

        RuleFor(x => x.StartDate!.Value)
            .NotEqual(default(DateOnly))
            .When(x => x.StartDate.HasValue);

        // Cross-field check é feito no service (precisa do start_date existente)
    }
}
