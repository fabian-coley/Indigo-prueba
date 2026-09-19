using Indigo.Api.Validation;
using Indigo.Application.Contracts.Reportes;
using Indigo.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Indigo.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly SalesReportService _reportes;

    public ReportsController(SalesReportService reportes)
    {
        _reportes = reportes;
    }

    [HttpGet("sales")]
    [ProducesResponseType(typeof(ReporteVentasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ventas(
        [FromQuery(Name = "from")] DateOnly? desde,
        [FromQuery(Name = "to")] DateOnly? hasta,
        CancellationToken ct)
    {
        if (desde is null || hasta is null)
        {
            return BadRequest(ValidacionDeRequest.Problema(
                desde is null ? "from" : "to",
                "Las fechas 'from' y 'to' son obligatorias, en formato YYYY-MM-DD."));
        }

        return Ok(await _reportes.GenerarAsync(desde.Value, hasta.Value, ct));
    }
}
