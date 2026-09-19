using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Indigo.Api.IntegrationTests.Infraestructura;
using Indigo.Application.Common;
using Indigo.Application.Contracts.Productos;
using Indigo.Application.Contracts.Ventas;
using Indigo.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Indigo.Api.IntegrationTests;

public sealed class VentasTests : IClassFixture<IndigoApiFactory>
{
    private readonly IndigoApiFactory _fabrica;

    public VentasTests(IndigoApiFactory fabrica) => _fabrica = fabrica;


    [Fact]
    public async Task Registrar_ConDatosValidos_Devuelve201YDescuentaStock()
    {
        var admin = await _fabrica.ClienteAdminAsync();
        var (vendedor, email) = await _fabrica.ClienteVendedorNuevoAsync();
        var producto = await CrearProductoAsync(admin, stock: 10);

        var respuesta = await vendedor.PostAsJsonAsync(
            "/api/sales",
            new RegistrarVentaRequest([new RegistrarVentaItemRequest(producto.Id, 3)]),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var venta = await respuesta.Content.ReadFromJsonAsync<VentaDetalleDto>(OpcionesJson.Valor);

        venta!.Id.Should().NotBeEmpty();
        venta.CantidadItems.Should().Be(3);
        venta.Total.Should().Be(producto.Precio * 3);
        venta.UsuarioEmail.Should().Be(email, "el usuario sale del token");

        var item = venta.Items.Should().ContainSingle().Subject;

        item.ProductoId.Should().Be(producto.Id);
        item.Cantidad.Should().Be(3);
        item.PrecioUnitario.Should().Be(producto.Precio);
        item.Subtotal.Should().Be(producto.Precio * 3);

        respuesta.Headers.Location.Should().NotBeNull();
        respuesta.Headers.Location!.AbsolutePath.Should().Be($"/api/sales/{venta.Id}");

        (await ObtenerProductoAsync(admin, producto.Id)).Stock.Should().Be(7, "la venta descuenta 3 de 10");
    }

    [Fact]
    public async Task Registrar_ConStockInsuficiente_Devuelve400YNoDescuenta()
    {
        var admin = await _fabrica.ClienteAdminAsync();
        var (vendedor, _) = await _fabrica.ClienteVendedorNuevoAsync();
        var producto = await CrearProductoAsync(admin, stock: 0);

        var respuesta = await vendedor.PostAsJsonAsync(
            "/api/sales",
            new RegistrarVentaRequest([new RegistrarVentaItemRequest(producto.Id, 1)]),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be(
            $"Stock insuficiente para '{producto.Nombre}': se solicitaron 1 unidad(es) y hay 0 disponible(s).");

        (await ObtenerProductoAsync(admin, producto.Id)).Stock.Should().Be(0);
    }

    [Fact]
    public async Task Registrar_ConUnItemSinStock_NoDejaRastroDelItemQueSiTenia()
    {
        var admin = await _fabrica.ClienteAdminAsync();
        var (vendedor, _) = await _fabrica.ClienteVendedorNuevoAsync();

        var conStock = await CrearProductoAsync(admin, stock: 5);
        var sinStock = await CrearProductoAsync(admin, stock: 0);

        var respuesta = await vendedor.PostAsJsonAsync(
            "/api/sales",
            new RegistrarVentaRequest(
            [
                new RegistrarVentaItemRequest(conStock.Id, 2),
                new RegistrarVentaItemRequest(sinStock.Id, 1),
            ]),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Contain("Stock insuficiente");

        (await ObtenerProductoAsync(admin, conStock.Id)).Stock
            .Should().Be(5, "el rollback devuelve el stock del ítem que sí se había descontado");
    }

    [Fact]
    public async Task Registrar_ConProductoInexistente_Devuelve400()
    {
        var (vendedor, _) = await _fabrica.ClienteVendedorNuevoAsync();
        var id = Guid.NewGuid();

        var respuesta = await vendedor.PostAsJsonAsync(
            "/api/sales",
            new RegistrarVentaRequest([new RegistrarVentaItemRequest(id, 1)]),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be($"No existe el producto con id '{id}'.");
    }


    [Fact]
    public async Task Registrar_ConCuerpoVacio_Devuelve400ConErrorDeItems()
    {
        var (vendedor, _) = await _fabrica.ClienteVendedorNuevoAsync();

        var respuesta = await vendedor.PostAsync(
            "/api/sales",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        var problema = await LeerProblemaDeValidacionAsync(respuesta, "items");

        problema.Errors["items"].Should().ContainSingle()
            .Which.Should().Be("La venta debe incluir al menos un ítem.");
    }

    [Fact]
    public async Task Registrar_ConListaDeItemsVacia_Devuelve400ConErrorDeItems()
    {
        var (vendedor, _) = await _fabrica.ClienteVendedorNuevoAsync();

        var respuesta = await vendedor.PostAsync(
            "/api/sales",
            new StringContent("""{"items":[]}""", Encoding.UTF8, "application/json"));

        var problema = await LeerProblemaDeValidacionAsync(respuesta, "items");

        problema.Errors["items"].Should().ContainSingle()
            .Which.Should().Be("La venta debe incluir al menos un ítem.");
    }

    [Fact]
    public async Task Registrar_ConCantidadCero_Devuelve400PorReglaDeNegocio()
    {
        var admin = await _fabrica.ClienteAdminAsync();
        var (vendedor, _) = await _fabrica.ClienteVendedorNuevoAsync();
        var producto = await CrearProductoAsync(admin, stock: 5);

        var respuesta = await vendedor.PostAsJsonAsync(
            "/api/sales",
            new RegistrarVentaRequest([new RegistrarVentaItemRequest(producto.Id, 0)]),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be("La cantidad de cada ítem de la venta debe ser mayor que cero.");

        (await ObtenerProductoAsync(admin, producto.Id)).Stock.Should().Be(5);
    }

    [Fact]
    public async Task Registrar_ConUsuarioIdEnElCuerpo_LoIgnoraYUsaElDelToken()
    {
        var admin = await _fabrica.ClienteAdminAsync();
        var (vendedor, email) = await _fabrica.ClienteVendedorNuevoAsync();
        var producto = await CrearProductoAsync(admin, stock: 5);

        var cuerpo = $$"""
            {
              "items": [{ "productoId": "{{producto.Id}}", "cantidad": 2 }],
              "usuarioId": "{{DatosSembrados.EmailAdmin}}"
            }
            """;

        var respuesta = await vendedor.PostAsync(
            "/api/sales",
            new StringContent(cuerpo, Encoding.UTF8, "application/json"));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var venta = await respuesta.Content.ReadFromJsonAsync<VentaDetalleDto>(OpcionesJson.Valor);

        venta!.UsuarioEmail.Should().Be(email, "el usuarioId del cuerpo se ignora");
    }


    [Fact]
    public async Task Listar_ComoVendedor_SoloDevuelveLasPropias()
    {
        var admin = await _fabrica.ClienteAdminAsync();
        var (vendedorA, emailA) = await _fabrica.ClienteVendedorNuevoAsync();
        var (vendedorB, emailB) = await _fabrica.ClienteVendedorNuevoAsync();
        var producto = await CrearProductoAsync(admin, stock: 20);

        await RegistrarVentaAsync(vendedorA, producto.Id, 1);
        await RegistrarVentaAsync(vendedorB, producto.Id, 1);

        var paginaA = await LeerVentasAsync(vendedorA);

        paginaA.Items.Should().ContainSingle();
        paginaA.Items[0].UsuarioEmail.Should().Be(emailA);
        paginaA.TotalItems.Should().Be(1);

        var paginaB = await LeerVentasAsync(vendedorB);

        paginaB.Items.Should().ContainSingle();
        paginaB.Items[0].UsuarioEmail.Should().Be(emailB);

        var paginaAdmin = await LeerVentasAsync(admin);

        paginaAdmin.Items.Select(venta => venta.Id)
            .Should().Contain(new[] { paginaA.Items[0].Id, paginaB.Items[0].Id });
    }

    [Fact]
    public async Task ObtenerPorId_DeOtroVendedor_Devuelve404PeroElAdminSiLaVe()
    {
        var admin = await _fabrica.ClienteAdminAsync();
        var (vendedorA, _) = await _fabrica.ClienteVendedorNuevoAsync();
        var (vendedorB, _) = await _fabrica.ClienteVendedorNuevoAsync();
        var producto = await CrearProductoAsync(admin, stock: 10);

        var venta = await RegistrarVentaAsync(vendedorA, producto.Id, 1);

        var ajena = await vendedorB.GetAsync($"/api/sales/{venta.Id}");

        ajena.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problema = await ajena.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be($"No existe una venta accesible con id {venta.Id}.");

        var propia = await vendedorA.GetAsync($"/api/sales/{venta.Id}");

        propia.StatusCode.Should().Be(HttpStatusCode.OK);

        var laDelAdmin = await admin.GetAsync($"/api/sales/{venta.Id}");

        laDelAdmin.StatusCode.Should().Be(HttpStatusCode.OK);

        var leida = await laDelAdmin.Content.ReadFromJsonAsync<VentaDetalleDto>(OpcionesJson.Valor);

        leida.Should().BeEquivalentTo(venta);
    }

    [Fact]
    public async Task ObtenerPorId_ConIdInexistente_Devuelve404()
    {
        var (vendedor, _) = await _fabrica.ClienteVendedorNuevoAsync();
        var id = Guid.NewGuid();

        var respuesta = await vendedor.GetAsync($"/api/sales/{id}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be($"No existe una venta accesible con id {id}.");
    }


    private static async Task<ProductoDto> CrearProductoAsync(HttpClient admin, int stock)
    {
        var respuesta = await admin.PostAsJsonAsync(
            "/api/products",
            new CrearProductoRequest($"Producto {Guid.NewGuid():N}", 25.50m, stock, CategoriaProducto.Otros),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await respuesta.Content.ReadFromJsonAsync<ProductoDto>(OpcionesJson.Valor))!;
    }

    private static async Task<ProductoDto> ObtenerProductoAsync(HttpClient cliente, Guid id)
    {
        var respuesta = await cliente.GetAsync($"/api/products/{id}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await respuesta.Content.ReadFromJsonAsync<ProductoDto>(OpcionesJson.Valor))!;
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

    private static async Task<PagedResult<VentaDto>> LeerVentasAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync("/api/sales");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await respuesta.Content.ReadFromJsonAsync<PagedResult<VentaDto>>(OpcionesJson.Valor))!;
    }

    private static async Task<ValidationProblemDetails> LeerProblemaDeValidacionAsync(
        HttpResponseMessage respuesta,
        string campo)
    {
        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ValidationProblemDetails>(OpcionesJson.Valor);

        problema!.Errors.Should().ContainKey(campo);

        return problema;
    }
}
