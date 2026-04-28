using BillFolder.Application.Dtos.Recurrences;
using FluentValidation;

namespace BillFolder.Application.Validators.Recurrences;

public class CreateCardEntryRecurrenceRequestValidator : AbstractValidator<CreateCardEntryRecurrenceRequest>
{
    public CreateCardEntryRecurrenceRequestValidator()
    {
        RuleFor(x => x.CardId).NotEqual(Guid.Empty)
            .WithMessage("Cartão é obrigatório.");

        RuleFor(x => x.DefaultLabel).NotEmpty().MaximumLength(200);

        RuleFor(x => x.DefaultAmount).GreaterThan(0)
            .WithMessage("Valor padrão deve ser maior que zero.");

        RuleFor(x => x.DefaultCategoryId).NotEqual(Guid.Empty);

        RuleFor(x => x.DayOfMonth).InclusiveBetween((short)1, (short)31)
            .WithMessage("Dia do mês deve estar entre 1 e 31.");

        RuleFor(x => x.StartDate).NotEqual(default(DateOnly));

        RuleFor(x => x).Must(x => x.EndDate!.Value >= x.StartDate)
            .WithMessage("Data de fim deve ser igual ou posterior à de início.")
            .When(x => x.EndDate.HasValue);
    }
}

public class UpdateCardEntryRecurrenceRequestValidator : AbstractValidator<UpdateCardEntryRecurrenceRequest>
{
    public UpdateCardEntryRecurrenceRequestValidator()
    {
        RuleFor(x => x.CardId!.Value).NotEqual(Guid.Empty)
            .When(x => x.CardId.HasValue);

        RuleFor(x => x.DefaultLabel!).NotEmpty().MaximumLength(200)
            .When(x => x.DefaultLabel is not null);

        RuleFor(x => x.DefaultAmount!.Value).GreaterThan(0)
            .When(x => x.DefaultAmount.HasValue)
            .WithMessage("Valor padrão deve ser maior que zero.");

        RuleFor(x => x.DefaultCategoryId!.Value).NotEqual(Guid.Empty)
            .When(x => x.DefaultCategoryId.HasValue);

        RuleFor(x => x.DayOfMonth!.Value).InclusiveBetween((short)1, (short)31)
            .When(x => x.DayOfMonth.HasValue);

        RuleFor(x => x.StartDate!.Value).NotEqual(default(DateOnly))
            .When(x => x.StartDate.HasValue);
    }
}
