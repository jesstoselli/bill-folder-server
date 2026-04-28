using BillFolder.Application.Dtos.CreditCards;
using FluentValidation;

namespace BillFolder.Application.Validators.CreditCards;

public class CreateCreditCardAccountRequestValidator : AbstractValidator<CreateCreditCardAccountRequest>
{
    public CreateCreditCardAccountRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nome do cartão é obrigatório.")
            .MaximumLength(50);

        RuleFor(x => x.IssuerBank!)
            .MaximumLength(50)
            .When(x => x.IssuerBank is not null);

        RuleFor(x => x.Brand!)
            .MaximumLength(30)
            .When(x => x.Brand is not null);

        RuleFor(x => x.ClosingDay)
            .InclusiveBetween((short)1, (short)31)
            .WithMessage("Dia de fechamento deve estar entre 1 e 31.");

        RuleFor(x => x.DueDay)
            .InclusiveBetween((short)1, (short)31)
            .WithMessage("Dia de vencimento deve estar entre 1 e 31.");
    }
}

public class UpdateCreditCardAccountRequestValidator : AbstractValidator<UpdateCreditCardAccountRequest>
{
    public UpdateCreditCardAccountRequestValidator()
    {
        RuleFor(x => x.Name!)
            .NotEmpty().MaximumLength(50)
            .When(x => x.Name is not null);

        RuleFor(x => x.IssuerBank!)
            .MaximumLength(50)
            .When(x => x.IssuerBank is not null);

        RuleFor(x => x.Brand!)
            .MaximumLength(30)
            .When(x => x.Brand is not null);

        RuleFor(x => x.ClosingDay!.Value)
            .InclusiveBetween((short)1, (short)31)
            .When(x => x.ClosingDay.HasValue)
            .WithMessage("Dia de fechamento deve estar entre 1 e 31.");

        RuleFor(x => x.DueDay!.Value)
            .InclusiveBetween((short)1, (short)31)
            .When(x => x.DueDay.HasValue)
            .WithMessage("Dia de vencimento deve estar entre 1 e 31.");
    }
}
