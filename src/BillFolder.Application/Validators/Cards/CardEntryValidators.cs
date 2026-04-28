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

        RuleFor(x => x.TotalAmount)
            .GreaterThan(0)
            .WithMessage("Valor total deve ser maior que zero.");

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
