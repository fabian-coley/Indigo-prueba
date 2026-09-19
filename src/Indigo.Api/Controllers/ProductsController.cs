using FluentValidation;
using Indigo.Api.Validation;
using Indigo.Application.Common;
using Indigo.Application.Contracts.Productos;
using Indigo.Application.Services;
using Indigo.Domain.Constants;
using Indigo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Indigo.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly ProductService _productos;
    private readonly ProductImageService _imagenes;
    private readonly IValidator<CrearProductoRequest> _validadorDeCreacion;
    private readonly IValidator<ActualizarProductoRequest> _validadorDeActualizacion;

    public ProductsController(
        ProductService productos,
        ProductImageService imagenes,
        IValidator<CrearProductoRequest> validadorDeCreacion,
        IValidator<ActualizarProductoRequest> validadorDeActualizacion)
    {
        _productos = productos;
        _imagenes = imagenes;
        _validadorDeCreacion = validadorDeCreacion;
        _validadorDeActualizacion = validadorDeActualizacion;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paginacion.PageSizePredeterminado,
        [FromQuery] string? search = null,
        [FromQuery] CategoriaProducto? categoria = null,
        CancellationToken ct = default)
    {
        var filtro = new ProductoFiltro(search, categoria, page, pageSize);

        return Ok(await _productos.ListarAsync(filtro, ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorId(Guid id, CancellationToken ct)
    {
        var producto = await _productos.ObtenerPorIdAsync(id, ct);

        return producto is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso no encontrado",
                detail: $"No existe un producto activo con id {id}.")
            : Ok(producto);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] CrearProductoRequest peticion, CancellationToken ct)
    {
        var problema = await ValidacionDeRequest.ValidarAsync(_validadorDeCreacion, peticion, ct);
        if (problema is not null)
        {
            return BadRequest(problema);
        }

        var producto = await _productos.CrearAsync(peticion, ct);

        return CreatedAtAction(nameof(ObtenerPorId), new { id = producto.Id }, producto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarProductoRequest peticion, CancellationToken ct)
    {
        var problema = await ValidacionDeRequest.ValidarAsync(_validadorDeActualizacion, peticion, ct);
        if (problema is not null)
        {
            return BadRequest(problema);
        }

        return Ok(await _productos.ActualizarAsync(id, peticion, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        await _productos.EliminarAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/imagen")]
    [Authorize(Roles = Roles.Admin)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProductoImagenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubirImagen(Guid id, IFormFile? file, CancellationToken ct)
    {

        if (file is null || file.Length == 0)
        {
            return BadRequest(ValidacionDeRequest.Problema("file", "El archivo es obligatorio."));
        }

        using var contenido = file.OpenReadStream();

        var imagen = await _imagenes.SubirAsync(id, contenido, file.FileName, file.Length, ct);

        return Ok(imagen);
    }
}
