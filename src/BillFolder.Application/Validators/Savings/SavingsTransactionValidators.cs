using BillFolder.Application.Dtos.Savings;
using FluentValidation;

namespace BillFolder.Application.Validators.Savings;

public class CreateSavingsTransactionRequestValidator : AbstractValidator<CreateSavingsTransactionRequest>
{
    public CreateSavingsTransactionRequestValidator()
    {
        RuleFor(x => x.SavingsAccountId)
            .NotEqual(Guid.Empty)
            .WithMessage("Poupança é obrigatória.");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Valor não pode ser negativo.");

        RuleFor(x => x.Date)
            .NotEqual(default(DateOnly))
            .WithMessage("Data é obrigatória.");

        RuleFor(x => x.Label!)
            .MaximumLength(200)
            .When(x => x.Label is not null);
    }
}

public class UpdateSavingsTransactionRequestValidator : AbstractValidator<UpdateSavingsTransactionRequest>
{
    public UpdateSavingsTransactionRequestValidator()
    {
        RuleFor(x => x.Amount!.Value)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Amount.HasValue)
            .WithMessage("Valor não pode ser negativo.");

        RuleFor(x => x.Date!.Value)
            .NotEqual(default(DateOnly))
            .When(x => x.Date.HasValue);

        RuleFor(x => x.Label!)
            .MaximumLength(200)
            .When(x => x.Label is not null);
    }
}
