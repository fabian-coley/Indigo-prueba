using FluentValidation;
using Indigo.Api.Validation;
using Indigo.Application.Common;
using Indigo.Application.Contracts.Ventas;
using Indigo.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Indigo.Api.Controllers;

[ApiController]
[Route("api/sales")]
public sealed class SalesController : ControllerBase
{
    private readonly SaleService _ventas;
    private readonly IValidator<RegistrarVentaRequest> _validador;

    public SalesController(SaleService ventas, IValidator<RegistrarVentaRequest> validador)
    {
        _ventas = ventas;
        _validador = validador;
    }

    [HttpPost]
    [ProducesResponseType(typeof(VentaDetalleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Registrar([FromBody] RegistrarVentaRequest peticion, CancellationToken ct)
    {
        var problema = await ValidacionDeRequest.ValidarAsync(_validador, peticion, ct);
        if (problema is not null)
        {
            return BadRequest(problema);
        }

        var venta = await _ventas.RegistrarAsync(peticion, ct);

        return CreatedAtAction(nameof(ObtenerPorId), new { id = venta.Id }, venta);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VentaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paginacion.PageSizePredeterminado,
        CancellationToken ct = default)
    {
        return Ok(await _ventas.ListarAsync(page, pageSize, ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VentaDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorId(Guid id, CancellationToken ct)
    {
        var venta = await _ventas.ObtenerPorIdAsync(id, ct);

        return venta is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado",
                detail: $"No existe una venta accesible con id {id}.")
            : Ok(venta);
    }
}
