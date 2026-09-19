using Indigo.Application.Common;
using Indigo.Application.Contracts.Ventas;
using Indigo.Domain.Entities;

namespace Indigo.Application.Mapping;

internal static class VentaMapping
{
    public static VentaDto ToDto(this VentaResumen resumen) => new(
        resumen.Id,
        resumen.Fecha,
        resumen.Total,
        resumen.CantidadItems,
        resumen.UsuarioEmail);

    public static PagedResult<VentaDto> ToDto(this PagedResult<VentaResumen> pagina) => new(
        [.. pagina.Items.Select(ToDto)],
        pagina.Page,
        pagina.PageSize,
        pagina.TotalItems);

    public static VentaItemDto ToDto(this SaleItem item) => new(
        item.ProductId,
        item.ProductoNombre,
        item.Cantidad,
        item.PrecioUnitario,
        item.Subtotal);

    public static VentaDetalleDto ToDetalle(this Sale venta, string usuarioEmail) => new(
        venta.Id,
        venta.Fecha,
        venta.Total,
        venta.CantidadItems,
        usuarioEmail,
        [.. venta.Items.Select(ToDto)]);
}
