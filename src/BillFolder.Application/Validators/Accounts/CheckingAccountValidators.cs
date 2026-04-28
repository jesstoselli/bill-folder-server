using BillFolder.Application.Dtos.Accounts;
using FluentValidation;

namespace BillFolder.Application.Validators.Accounts;

public class CreateCheckingAccountRequestValidator : AbstractValidator<CreateCheckingAccountRequest>
{
    public CreateCheckingAccountRequestValidator()
    {
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

public class UpdateCheckingAccountRequestValidator : AbstractValidator<UpdateCheckingAccountRequest>
{
    public UpdateCheckingAccountRequestValidator()
    {
        RuleFor(x => x.BankName!)
            .NotEmpty().MaximumLength(100)
            .When(x => x.BankName is not null);

        // Update parcial: se branch foi enviado, não pode ser vazio
        RuleFor(x => x.Branch!)
            .NotEmpty().WithMessage("Agência não pode ser vazia.")
            .MaximumLength(20)
            .When(x => x.Branch is not null);

        RuleFor(x => x.AccountNumber!)
            .NotEmpty().WithMessage("Número da conta não pode ser vazio.")
            .MaximumLength(30)
            .When(x => x.AccountNumber is not null);

        RuleFor(x => x.InitialBalance!.Value)
            .GreaterThanOrEqualTo(0)
            .When(x => x.InitialBalance.HasValue)
            .WithMessage("Saldo inicial não pode ser negativo.");
    }
}
