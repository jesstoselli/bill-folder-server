using BillFolder.Application.Dtos.Savings;
using FluentValidation;

namespace BillFolder.Application.Validators.Savings;

public class CreateSavingsAccountRequestValidator : AbstractValidator<CreateSavingsAccountRequest>
{
    public CreateSavingsAccountRequestValidator()
    {
        RuleFor(x => x.CheckingAccountId)
            .NotEqual(Guid.Empty)
            .WithMessage("Conta corrente associada é obrigatória.");

        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage("Banco é obrigatório.")
            .MaximumLength(100);

        RuleFor(x => x.Branch)
            .NotEmpty().WithMessage("Agência é obrigatória.")
            .MaximumLength(20);

        RuleFor(x => x.AccountNumber)
            .NotEmpty().WithMessage("Número da conta é obrigatório.")
            .MaximumLength(30);

        RuleFor(x => x.InitialBalance)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Saldo inicial não pode ser negativo.");
    }
}

public class UpdateSavingsAccountRequestValidator : AbstractValidator<UpdateSavingsAccountRequest>
{
    public UpdateSavingsAccountRequestValidator()
    {
        RuleFor(x => x.BankName!)
            .NotEmpty().MaximumLength(100)
            .When(x => x.BankName is not null);

        RuleFor(x => x.Branch!)
            .NotEmpty().MaximumLength(20)
            .When(x => x.Branch is not null);

        RuleFor(x => x.AccountNumber!)
            .NotEmpty().MaximumLength(30)
            .When(x => x.AccountNumber is not null);

        RuleFor(x => x.InitialBalance!.Value)
            .GreaterThanOrEqualTo(0)
            .When(x => x.InitialBalance.HasValue)
            .WithMessage("Saldo inicial não pode ser negativo.");
    }
}
