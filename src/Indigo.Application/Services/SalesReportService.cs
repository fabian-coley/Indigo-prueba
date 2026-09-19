using Indigo.Application.Abstractions.Persistence;
using Indigo.Application.Abstractions.Security;
using Indigo.Application.Common;
using Indigo.Application.Contracts.Reportes;
using Indigo.Domain.Exceptions;

namespace Indigo.Application.Services;

public class SalesReportService
{
    public const int DiasMaximosDeRango = 366;

    private readonly ISaleRepository _ventas;
    private readonly ICurrentUserService _usuarioActual;

    public SalesReportService(ISaleRepository ventas, ICurrentUserService usuarioActual)
    {
        _ventas = ventas;
        _usuarioActual = usuarioActual;
    }

    public async Task<ReporteVentasDto> GenerarAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
    {
        if (desde > hasta)
        {
            throw new ExcepcionReglaDeNegocio("La fecha 'from' no puede ser posterior a la fecha 'to'.");
        }

        if (hasta.DayNumber - desde.DayNumber + 1 > DiasMaximosDeRango)
        {
            throw new ExcepcionReglaDeNegocio($"El rango del reporte no puede superar los {DiasMaximosDeRango} días.");
        }

        var desdeUtc = new DateTimeOffset(desde.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var hastaExclusivo = new DateTimeOffset(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var usuarioId = _usuarioActual.EsAdmin ? null : _usuarioActual.UsuarioIdRequerido();

        var ventas = await _ventas.GetPorRangoAsync(desdeUtc, hastaExclusivo, usuarioId, ct);

        var porDia = ventas
            .GroupBy(venta => DateOnly.FromDateTime(venta.Fecha.UtcDateTime))
            .OrderBy(grupo => grupo.Key)
            .Select(grupo => new ReporteVentasPorDiaDto(
                grupo.Key,
                grupo.Count(),
                grupo.Sum(venta => venta.CantidadItems),
                grupo.Sum(venta => venta.Total)))
            .ToList();

        return new ReporteVentasDto(
            desde,
            hasta,
            ventas.Sum(venta => venta.Total),
            ventas.Count,
            ventas.Sum(venta => venta.CantidadItems),
            porDia);
    }
}
