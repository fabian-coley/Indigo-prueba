namespace Indigo.Application.Contracts.Ventas;

public sealed record VentaDetalleDto(
    Guid Id,
    DateTimeOffset Fecha,
    decimal Total,
    int CantidadItems,
    string UsuarioEmail,
    IReadOnlyList<VentaItemDto> Items);
