namespace Indigo.Application.Contracts.Reportes;

public sealed record ReporteVentasDto(
    DateOnly Desde,
    DateOnly Hasta,
    decimal TotalGeneral,
    int TotalVentas,
    int TotalItems,
    IReadOnlyList<ReporteVentasPorDiaDto> PorDia);
