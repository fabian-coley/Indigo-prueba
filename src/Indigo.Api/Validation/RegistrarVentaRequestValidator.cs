using FluentValidation;
using Indigo.Application.Contracts.Ventas;

namespace Indigo.Api.Validation;

public sealed class RegistrarVentaRequestValidator : AbstractValidator<RegistrarVentaRequest>
{
    private const string MensajeDeItems = "La venta debe incluir al menos un ítem.";

    public RegistrarVentaRequestValidator()
    {
        RuleFor(request => request.Items)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(MensajeDeItems)
            .NotEmpty().WithMessage(MensajeDeItems);

        RuleForEach(request => request.Items)
            .NotNull().WithMessage("La venta no puede incluir ítems nulos.");
    }
}
