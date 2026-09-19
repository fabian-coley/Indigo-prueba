using Indigo.Application.Common;
using Indigo.Application.Contracts.Productos;
using Indigo.Domain.Entities;

namespace Indigo.Application.Mapping;

internal static class ProductoMapping
{
    public static ProductoDto ToDto(this Product producto) => new(
        producto.Id,
        producto.Nombre,
        producto.Precio,
        producto.Stock,
        producto.Categoria,
        producto.ImagenUrl);

    public static PagedResult<ProductoDto> ToDto(this PagedResult<Product> pagina) => new(
        [.. pagina.Items.Select(ToDto)],
        pagina.Page,
        pagina.PageSize,
        pagina.TotalItems);
}
