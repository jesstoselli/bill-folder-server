using BillFolder.Application.Dtos.Expenses;
using FluentValidation;

namespace BillFolder.Application.Validators.Expenses;

public class CreateExpenseRequestValidator : AbstractValidator<CreateExpenseRequest>
{
    public CreateExpenseRequestValidator()
    {
        RuleFor(x => x.DueDate)
            .NotEqual(default(DateOnly))
            .WithMessage("Data de vencimento é obrigatória.");

        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("Descrição é obrigatória.")
            .MaximumLength(200);

        RuleFor(x => x.ExpectedAmount)
            .GreaterThan(0)
            .WithMessage("Valor esperado deve ser maior que zero.");

        RuleFor(x => x.CategoryId)
            .NotEqual(Guid.Empty)
            .WithMessage("Categoria é obrigatória.");

        RuleFor(x => x.Notes!)
            .MaximumLength(500)
            .When(x => x.Notes is not null);
    }
}

public class UpdateExpenseRequestValidator : AbstractValidator<UpdateExpenseRequest>
{
    public UpdateExpenseRequestValidator()
    {
        RuleFor(x => x.DueDate!.Value)
            .NotEqual(default(DateOnly))
            .When(x => x.DueDate.HasValue);

        RuleFor(x => x.Label!)
            .NotEmpty().MaximumLength(200)
            .When(x => x.Label is not null);

        RuleFor(x => x.ExpectedAmount!.Value)
            .GreaterThan(0)
            .When(x => x.ExpectedAmount.HasValue)
            .WithMessage("Valor esperado deve ser maior que zero.");

        RuleFor(x => x.ActualAmount!.Value)
            .GreaterThan(0)
            .When(x => x.ActualAmount.HasValue)
            .WithMessage("Valor pago deve ser maior que zero.");

        RuleFor(x => x.PaidFromAccountId!.Value)
            .NotEqual(Guid.Empty)
            .When(x => x.PaidFromAccountId.HasValue);

        RuleFor(x => x.CategoryId!.Value)
            .NotEqual(Guid.Empty)
            .When(x => x.CategoryId.HasValue);

        RuleFor(x => x.Notes!)
            .MaximumLength(500)
            .When(x => x.Notes is not null);
    }
}
