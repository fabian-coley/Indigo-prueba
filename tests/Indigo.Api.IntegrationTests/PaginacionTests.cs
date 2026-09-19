using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Indigo.Api.IntegrationTests.Infraestructura;
using Indigo.Application.Common;
using Indigo.Application.Contracts.Productos;
using Indigo.Application.Contracts.Ventas;
using Microsoft.AspNetCore.Mvc;

namespace Indigo.Api.IntegrationTests;

public sealed class PaginacionTests : IClassFixture<IndigoApiFactory>
{
    private static readonly string[] CatalogoOrdenado =
    [
        "Auriculares Bluetooth",
        "Cafetera Express",
        "Cámara Digital",
        "Detergente Líquido 1L",
        "Harina de Trigo 1kg",
        "Lámpara de Escritorio",
        "Monitor LED 27 Pulgadas",
        "Pilas AA Pack x4",
        "Silla Ergonómica",
        "Teclado Mecánico",
        "Yerba Mate 1kg",
        "Zapatillas Running",
    ];

    private readonly IndigoApiFactory _fabrica;

    public PaginacionTests(IndigoApiFactory fabrica) => _fabrica = fabrica;

    [Fact]
    public async Task ListarProductos_SinParametros_DevuelveLaPrimeraPaginaConDiez()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var pagina = await LeerPaginaDeProductosAsync(cliente, "/api/products");

        pagina.Page.Should().Be(1);
        pagina.PageSize.Should().Be(10);
        pagina.TotalItems.Should().Be(12);
        pagina.TotalPages.Should().Be(2);
        pagina.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task ListarProductos_SegundaPagina_DevuelveElResto()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var primera = await LeerPaginaDeProductosAsync(cliente, "/api/products");
        var segunda = await LeerPaginaDeProductosAsync(cliente, "/api/products?page=2");

        segunda.Page.Should().Be(2);
        segunda.TotalItems.Should().Be(12);
        segunda.Items.Should().HaveCount(2);

        var ids = primera.Items.Concat(segunda.Items).Select(producto => producto.Id).ToList();

        ids.Should().HaveCount(12);
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ListarProductos_OrdenaPorNombre()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var pagina = await LeerPaginaDeProductosAsync(cliente, "/api/products?pageSize=100");

        pagina.Items.Select(producto => producto.Nombre).Should().Equal(CatalogoOrdenado);
    }

    [Fact]
    public async Task ListarProductos_ConPageSizeMaximo_DevuelveTodoElCatalogo()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var pagina = await LeerPaginaDeProductosAsync(cliente, "/api/products?pageSize=100");

        pagina.PageSize.Should().Be(100);
        pagina.TotalPages.Should().Be(1);
        pagina.Items.Should().HaveCount(12);
    }

    [Fact]
    public async Task ListarProductos_MasAllaDeLaUltimaPagina_DevuelvePaginaVacia()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var pagina = await LeerPaginaDeProductosAsync(cliente, "/api/products?page=3");

        pagina.Items.Should().BeEmpty();
        pagina.TotalItems.Should().Be(12);
        pagina.TotalPages.Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task ListarProductos_ConPageSizeFueraDeRango_Devuelve400(int pageSize)
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.GetAsync($"/api/products?pageSize={pageSize}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be("El parámetro 'pageSize' debe estar entre 1 y 100.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task ListarProductos_ConPageMenorAUno_Devuelve400(int page)
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.GetAsync($"/api/products?page={page}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be("El parámetro 'page' debe ser mayor o igual a 1.");
    }

    [Fact]
    public async Task ListarVentas_ConPageFueraDeRango_Devuelve400()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.GetAsync("/api/sales?page=0");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be("El parámetro 'page' debe ser mayor o igual a 1.");
    }

    [Fact]
    public async Task ListarVentas_SinParametros_DevuelveLaEstructuraDePaginaDelContrato()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.GetAsync("/api/sales");
        var pagina = await respuesta.Content.ReadFromJsonAsync<PagedResult<VentaDto>>(OpcionesJson.Valor);

        pagina!.Page.Should().Be(1);
        pagina.PageSize.Should().Be(10);
    }

    private static async Task<PagedResult<ProductoDto>> LeerPaginaDeProductosAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await respuesta.Content.ReadFromJsonAsync<PagedResult<ProductoDto>>(OpcionesJson.Valor))!;
    }
}
