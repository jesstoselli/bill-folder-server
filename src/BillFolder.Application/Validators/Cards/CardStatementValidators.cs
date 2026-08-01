using BillFolder.Application.Dtos.Cards;
using FluentValidation;

namespace BillFolder.Application.Validators.Cards;

public class PayCardStatementRequestValidator : AbstractValidator<PayCardStatementRequest>
{
    public PayCardStatementRequestValidator()
    {
        RuleFor(x => x.PaidDate)
            .NotEqual(default(DateOnly))
            .WithMessage("Data do pagamento é obrigatória.");

        RuleFor(x => x.ActualAmount)
            .GreaterThan(0m)
            .WithMessage("Valor pago deve ser maior que zero.");
    }
}
