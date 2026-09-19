namespace Indigo.Application.Common;

public sealed record VentaResumen(
    Guid Id,
    DateTimeOffset Fecha,
    decimal Total,
    int CantidadItems,
    string UsuarioEmail);
