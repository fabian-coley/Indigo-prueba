namespace Indigo.Application.Contracts.Ventas;

public sealed record VentaDto(
    Guid Id,
    DateTimeOffset Fecha,
    decimal Total,
    int CantidadItems,
    string UsuarioEmail);
