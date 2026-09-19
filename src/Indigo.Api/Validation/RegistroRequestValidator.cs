using FluentValidation;
using Indigo.Application.Contracts.Auth;

namespace Indigo.Api.Validation;

public sealed class RegistroRequestValidator : AbstractValidator<RegistroRequest>
{
    public RegistroRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.");

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.");

        RuleFor(request => request.NombreCompleto)
            .NotEmpty().WithMessage("El nombre completo es obligatorio.");
    }
}
