namespace Indigo.Application.Contracts.Reportes;

public sealed record ReporteVentasPorDiaDto(
    DateOnly Fecha,
    int Ventas,
    int Items,
    decimal Total);
