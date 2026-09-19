using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Indigo.Api.IntegrationTests.Infraestructura;
using Indigo.Application.Common;
using Indigo.Application.Contracts.Productos;
using Indigo.Application.Contracts.Reportes;
using Indigo.Application.Contracts.Ventas;
using Indigo.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Indigo.Api.IntegrationTests;

public sealed class FlujoCompletoTests : IClassFixture<IndigoApiFactory>
{
    private const int ProductosSembrados = 12;

    private readonly IndigoApiFactory _fabrica;

    public FlujoCompletoTests(IndigoApiFactory fabrica) => _fabrica = fabrica;

    [Fact]
    public async Task RecorridoCompleto_DeLoginAReporteConBajaDelProducto()
    {
        var admin = await _fabrica.ClienteAdminAsync();

        var catalogo = await LeerPaginaAsync(admin, "/api/products?pageSize=100");

        catalogo.TotalItems.Should().Be(ProductosSembrados);
        catalogo.Items.Should().HaveCount(ProductosSembrados);

        var nombre = $" Producto de Flujo {Guid.NewGuid():N}";
        var precio = 120.00m;

        var alta = await admin.PostAsJsonAsync(
            "/api/products",
            new CrearProductoRequest(nombre, precio, 10, CategoriaProducto.Electrónica),
            OpcionesJson.Valor);

        alta.StatusCode.Should().Be(HttpStatusCode.Created);
        alta.Headers.Location.Should().NotBeNull();

        var producto = (await alta.Content.ReadFromJsonAsync<ProductoDto>(OpcionesJson.Valor))!;

        producto.Stock.Should().Be(10, "el alta trae el stock que se pidió");

        var detalle = await admin.GetAsync($"/api/products/{producto.Id}");
        detalle.StatusCode.Should().Be(HttpStatusCode.OK);

        var reciénCreado = (await detalle.Content.ReadFromJsonAsync<ProductoDto>(OpcionesJson.Valor))!;

        reciénCreado.Nombre.Should().Be(nombre.Trim());
        reciénCreado.Id.Should().Be(producto.Id);

        var venta = await RegistrarVentaAsync(admin, producto.Id, 2);

        venta.CantidadItems.Should().Be(2);
        venta.Total.Should().Be(precio * 2);
        venta.UsuarioEmail.Should().Be(DatosSembrados.EmailAdmin, "la venta queda a nombre del token");

        var item = venta.Items.Should().ContainSingle().Subject;

        item.ProductoId.Should().Be(producto.Id);
        item.ProductoNombre.Should().Be(nombre.Trim(), "el ítem guarda el nombre del producto al vender");
        item.Cantidad.Should().Be(2);
        item.PrecioUnitario.Should().Be(precio);
        item.Subtotal.Should().Be(precio * 2);

        (await LeerProductoAsync(admin, producto.Id)).Stock.Should().Be(8);

        var exceso = await admin.PostAsJsonAsync(
            "/api/sales",
            new RegistrarVentaRequest([new RegistrarVentaItemRequest(producto.Id, 999)]),
            OpcionesJson.Valor);

        exceso.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await exceso.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be(
            $"Stock insuficiente para '{nombre.Trim()}': se solicitaron 999 unidad(es) y hay 8 disponible(s).");

        (await LeerProductoAsync(admin, producto.Id)).Stock.Should().Be(8);

        var dia = DateOnly.FromDateTime(venta.Fecha.UtcDateTime);

        var reporte = await LeerReporteAsync(admin, dia);

        reporte.TotalVentas.Should().Be(1, "es la única venta de esta base");
        reporte.TotalItems.Should().Be(2);
        reporte.TotalGeneral.Should().Be(precio * 2);
        reporte.PorDia.Should().ContainSingle();

        reporte.PorDia[0].Fecha.Should().Be(dia);
        reporte.PorDia[0].Total.Should().Be(precio * 2);

        var baja = await admin.DeleteAsync($"/api/products/{producto.Id}");

        baja.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var despuesDeLaBaja = await admin.GetAsync($"/api/products/{producto.Id}");
        despuesDeLaBaja.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ventaHistorica = await admin.GetAsync($"/api/sales/{venta.Id}");

        ventaHistorica.StatusCode.Should().Be(HttpStatusCode.OK);

        var leida = (await ventaHistorica.Content.ReadFromJsonAsync<VentaDetalleDto>(OpcionesJson.Valor))!;

        leida.Items.Should().ContainSingle();
        leida.Items[0].ProductoNombre.Should().Be(nombre.Trim(), "el ítem guarda una copia del nombre");
        leida.Items[0].ProductoId.Should().Be(producto.Id, "el id sigue apuntando a la fila dada de baja");
        leida.Total.Should().Be(precio * 2);
    }


    private static async Task<ProductoDto> LeerProductoAsync(HttpClient cliente, Guid id)
    {
        var respuesta = await cliente.GetAsync($"/api/products/{id}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await respuesta.Content.ReadFromJsonAsync<ProductoDto>(OpcionesJson.Valor))!;
    }

    private static async Task<PagedResult<ProductoDto>> LeerPaginaAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await respuesta.Content.ReadFromJsonAsync<PagedResult<ProductoDto>>(OpcionesJson.Valor))!;
    }

    private static async Task<VentaDetalleDto> RegistrarVentaAsync(HttpClient cliente, Guid productoId, int cantidad)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/sales",
            new RegistrarVentaRequest([new RegistrarVentaItemRequest(productoId, cantidad)]),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await respuesta.Content.ReadFromJsonAsync<VentaDetalleDto>(OpcionesJson.Valor))!;
    }

    private static async Task<ReporteVentasDto> LeerReporteAsync(HttpClient cliente, DateOnly dia)
    {
        var respuesta = await cliente.GetAsync($"/api/reports/sales?from={dia:yyyy-MM-dd}&to={dia:yyyy-MM-dd}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await respuesta.Content.ReadFromJsonAsync<ReporteVentasDto>(OpcionesJson.Valor))!;
    }
}
