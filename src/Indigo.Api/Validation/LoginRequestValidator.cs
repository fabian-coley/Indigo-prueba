using FluentValidation;
using Indigo.Application.Contracts.Auth;

namespace Indigo.Api.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("El email es obligatorio.");

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}
