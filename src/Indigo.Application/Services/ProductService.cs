using Indigo.Application.Abstractions.Persistence;
using Indigo.Application.Abstractions.Storage;
using Indigo.Application.Common;
using Indigo.Application.Contracts.Productos;
using Indigo.Application.Mapping;
using Indigo.Domain.Entities;
using Indigo.Domain.Exceptions;

namespace Indigo.Application.Services;

public class ProductService
{
    private readonly IProductRepository _productos;
    private readonly IBlobStorage _almacenamiento;
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(IProductRepository productos, IBlobStorage almacenamiento, IUnitOfWork unitOfWork)
    {
        _productos = productos;
        _almacenamiento = almacenamiento;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<ProductoDto>> ListarAsync(ProductoFiltro filtro, CancellationToken ct = default)
    {
        Paginacion.Validar(filtro.Page, filtro.PageSize);

        var pagina = await _productos.GetPagedAsync(filtro, ct);

        return pagina.ToDto();
    }

    public async Task<ProductoDto?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default)
    {
        var producto = await _productos.GetByIdAsync(id, ct: ct);

        return producto?.ToDto();
    }

    public async Task<ProductoDto> CrearAsync(CrearProductoRequest request, CancellationToken ct = default)
    {
        var producto = new Product(request.Nombre, request.Precio, request.Stock, request.Categoria);

        _productos.Add(producto);
        await _unitOfWork.SaveChangesAsync(ct);

        return producto.ToDto();
    }

    public async Task<ProductoDto> ActualizarAsync(
        Guid id,
        ActualizarProductoRequest request,
        CancellationToken ct = default)
    {
        var producto = await _productos.GetByIdAsync(id, ct: ct)
            ?? throw new ExcepcionNoEncontrado($"No existe el producto con id '{id}'.");

        producto.Actualizar(request.Nombre, request.Precio, request.Stock, request.Categoria);

        _productos.Update(producto);
        await _unitOfWork.SaveChangesAsync(ct);

        return producto.ToDto();
    }

    public async Task EliminarAsync(Guid id, CancellationToken ct = default)
    {
        var producto = await _productos.GetByIdAsync(id, ct: ct)
            ?? throw new ExcepcionNoEncontrado($"No existe el producto con id '{id}'.");

        var imagen = producto.ImagenUrl;

        producto.Desactivar();
        _productos.Delete(producto);
        await _unitOfWork.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(imagen))
        {
            await _almacenamiento.EliminarAsync(imagen, ct);
        }
    }
}
