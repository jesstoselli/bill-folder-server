using BillFolder.Application.Dtos.Auth;
using FluentValidation;

namespace BillFolder.Application.Validators.Auth;

public class SignupRequestValidator : AbstractValidator<SignupRequest>
{
    public SignupRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email é obrigatório.")
            .EmailAddress().WithMessage("Email inválido.")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Senha é obrigatória.")
            .MinimumLength(8).WithMessage("Senha precisa ter no mínimo 8 caracteres.")
            .MaximumLength(128);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Nome é obrigatório.")
            .MinimumLength(2)
            .MaximumLength(100);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress().MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().MaximumLength(128);
    }
}
