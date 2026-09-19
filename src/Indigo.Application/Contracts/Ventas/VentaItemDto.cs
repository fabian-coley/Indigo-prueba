namespace Indigo.Application.Contracts.Ventas;

public sealed record VentaItemDto(
    Guid ProductoId,
    string ProductoNombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal);
