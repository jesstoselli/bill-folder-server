using BillFolder.Application.Dtos.Recurrences;
using FluentValidation;

namespace BillFolder.Application.Validators.Recurrences;

public class CreateExpenseRecurrenceRequestValidator : AbstractValidator<CreateExpenseRecurrenceRequest>
{
    public CreateExpenseRecurrenceRequestValidator()
    {
        RuleFor(x => x.DefaultLabel)
            .NotEmpty().MaximumLength(200);

        RuleFor(x => x.DefaultAmount)
            .GreaterThan(0)
            .WithMessage("Valor padrão deve ser maior que zero.");

        RuleFor(x => x.DefaultCategoryId)
            .NotEqual(Guid.Empty)
            .WithMessage("Categoria é obrigatória.");

        RuleFor(x => x.DueDay)
            .InclusiveBetween((short)1, (short)31)
            .WithMessage("Dia de vencimento deve estar entre 1 e 31.");

        RuleFor(x => x.StartDate)
            .NotEqual(default(DateOnly));

        RuleFor(x => x)
            .Must(x => x.EndDate!.Value >= x.StartDate)
            .WithMessage("Data de fim deve ser igual ou posterior à de início.")
            .When(x => x.EndDate.HasValue);
    }
}

public class UpdateExpenseRecurrenceRequestValidator : AbstractValidator<UpdateExpenseRecurrenceRequest>
{
    public UpdateExpenseRecurrenceRequestValidator()
    {
        RuleFor(x => x.DefaultLabel!)
            .NotEmpty().MaximumLength(200)
            .When(x => x.DefaultLabel is not null);

        RuleFor(x => x.DefaultAmount!.Value)
            .GreaterThan(0).When(x => x.DefaultAmount.HasValue)
            .WithMessage("Valor padrão deve ser maior que zero.");

        RuleFor(x => x.DefaultCategoryId!.Value)
            .NotEqual(Guid.Empty).When(x => x.DefaultCategoryId.HasValue);

        RuleFor(x => x.DueDay!.Value)
            .InclusiveBetween((short)1, (short)31)
            .When(x => x.DueDay.HasValue);

        RuleFor(x => x.StartDate!.Value)
            .NotEqual(default(DateOnly)).When(x => x.StartDate.HasValue);
    }
}
