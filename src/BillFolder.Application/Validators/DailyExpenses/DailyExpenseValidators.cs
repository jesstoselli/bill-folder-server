using BillFolder.Application.Dtos.DailyExpenses;
using FluentValidation;

namespace BillFolder.Application.Validators.DailyExpenses;

public class CreateDailyExpenseRequestValidator : AbstractValidator<CreateDailyExpenseRequest>
{
    public CreateDailyExpenseRequestValidator()
    {
        RuleFor(x => x.Date)
            .NotEqual(default(DateOnly))
            .WithMessage("Data é obrigatória.");

        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("Descrição é obrigatória.")
            .MaximumLength(200);

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Valor deve ser maior que zero.");

        RuleFor(x => x.CategoryId)
            .NotEqual(Guid.Empty)
            .WithMessage("Categoria é obrigatória.");

        RuleFor(x => x.AccountId)
            .NotEqual(Guid.Empty)
            .WithMessage("Conta é obrigatória.");

        RuleFor(x => x.Notes!)
            .MaximumLength(500)
            .When(x => x.Notes is not null);
    }
}

public class UpdateDailyExpenseRequestValidator : AbstractValidator<UpdateDailyExpenseRequest>
{
    public UpdateDailyExpenseRequestValidator()
    {
        RuleFor(x => x.Date!.Value)
            .NotEqual(default(DateOnly))
            .When(x => x.Date.HasValue);

        RuleFor(x => x.Label!)
            .NotEmpty().MaximumLength(200)
            .When(x => x.Label is not null);

        RuleFor(x => x.Amount!.Value)
            .GreaterThan(0)
            .When(x => x.Amount.HasValue)
            .WithMessage("Valor deve ser maior que zero.");

        RuleFor(x => x.CategoryId!.Value)
            .NotEqual(Guid.Empty)
            .When(x => x.CategoryId.HasValue);

        RuleFor(x => x.AccountId!.Value)
            .NotEqual(Guid.Empty)
            .When(x => x.AccountId.HasValue);

        RuleFor(x => x.Notes!)
            .MaximumLength(500)
            .When(x => x.Notes is not null);
    }
}
