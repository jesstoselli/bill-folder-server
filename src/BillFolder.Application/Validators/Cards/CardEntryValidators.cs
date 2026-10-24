using BillFolder.Application.Dtos.Cards;
using FluentValidation;

namespace BillFolder.Application.Validators.Cards;

public class CreateCardEntryRequestValidator : AbstractValidator<CreateCardEntryRequest>
{
    public CreateCardEntryRequestValidator()
    {
        RuleFor(x => x.CardId)
            .NotEqual(Guid.Empty)
            .WithMessage("Cartão é obrigatório.");

        RuleFor(x => x.PurchaseDate)
            .NotEqual(default(DateOnly))
            .WithMessage("Data da compra é obrigatória.");

        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("Descrição é obrigatória.")
            .MaximumLength(200);

        // Negativo é estorno: crédito no cartão, sempre à vista.
        RuleFor(x => x.TotalAmount)
            .NotEqual(0)
            .WithMessage("Valor total não pode ser zero.");

        RuleFor(x => x.InstallmentsCount)
            .Equal((short)1)
            .When(x => x.TotalAmount < 0)
            .WithMessage("Estorno é sempre à vista (1 parcela).");

        RuleFor(x => x.InstallmentsCount)
            .InclusiveBetween((short)1, (short)36)
            .WithMessage("Número de parcelas deve estar entre 1 e 36.");

        RuleFor(x => x.CategoryId)
            .NotEqual(Guid.Empty)
            .WithMessage("Categoria é obrigatória.");

        RuleFor(x => x.Notes!)
            .MaximumLength(500)
            .When(x => x.Notes is not null);
    }
}

public class UpdateCardEntryRequestValidator : AbstractValidator<UpdateCardEntryRequest>
{
    public UpdateCardEntryRequestValidator()
    {
        RuleFor(x => x.Label!)
            .NotEmpty().MaximumLength(200)
            .When(x => x.Label is not null);

        RuleFor(x => x.CategoryId!.Value)
            .NotEqual(Guid.Empty)
            .When(x => x.CategoryId.HasValue);

        RuleFor(x => x.Notes!)
            .MaximumLength(500)
            .When(x => x.Notes is not null);
    }
}

public class UpdateCardSubscriptionAmountRequestValidator
    : AbstractValidator<UpdateCardSubscriptionAmountRequest>
{
    public UpdateCardSubscriptionAmountRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Valor deve ser maior que zero.");
    }
}
