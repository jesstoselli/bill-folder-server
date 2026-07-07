using BillFolder.Application.Dtos.Recurrences;
using BillFolder.Domain.Enums;
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

        // Monthly usa o dia do mês (1–31); Weekly usa o dia da semana (0–6).
        RuleFor(x => x.DueDay)
            .NotNull().InclusiveBetween((short)1, (short)31)
            .When(x => x.Frequency == ExpenseRecurrenceFrequency.Monthly)
            .WithMessage("Dia de vencimento (1–31) é obrigatório para recorrência mensal.");

        RuleFor(x => x.Weekday)
            .NotNull().InclusiveBetween((short)0, (short)6)
            .When(x => x.Frequency == ExpenseRecurrenceFrequency.Weekly)
            .WithMessage("Dia da semana (0–6) é obrigatório para recorrência semanal.");

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

        RuleFor(x => x.Weekday!.Value)
            .InclusiveBetween((short)0, (short)6)
            .When(x => x.Weekday.HasValue);

        RuleFor(x => x.StartDate!.Value)
            .NotEqual(default(DateOnly)).When(x => x.StartDate.HasValue);
    }
}
