using FluentValidation;
using Indigo.Application.Contracts.Productos;

namespace Indigo.Api.Validation;

public sealed class ActualizarProductoRequestValidator : AbstractValidator<ActualizarProductoRequest>
{
    private const int LongitudMaximaDeNombre = 150;

    private const string MensajeDeNombreObligatorio = "El nombre del producto es obligatorio.";

    private static readonly string MensajeDeNombreLargo = $"El nombre del producto no puede superar los {LongitudMaximaDeNombre} caracteres.";
    private const string MensajeDePrecio = "El precio del producto debe ser mayor que cero.";
    private const string MensajeDeStock = "El stock del producto no puede ser negativo.";

    public ActualizarProductoRequestValidator()
    {
        RuleFor(request => request.Nombre)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(MensajeDeNombreObligatorio)
            .MaximumLength(LongitudMaximaDeNombre).WithMessage(MensajeDeNombreLargo);

        RuleFor(request => request.Precio)
            .GreaterThan(0m).WithMessage(MensajeDePrecio);

        RuleFor(request => request.Stock)
            .GreaterThanOrEqualTo(0).WithMessage(MensajeDeStock);

        RuleFor(request => request.Categoria)
            .IsInEnum().WithMessage(request => $"La categoría '{request.Categoria}' no es una categoría válida.");
    }
}
