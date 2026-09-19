using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Indigo.Api.IntegrationTests.Infraestructura;
using Indigo.Application.Contracts.Productos;
using Indigo.Domain.Enums;

namespace Indigo.Api.IntegrationTests;

public sealed class AutorizacionTests : IClassFixture<IndigoApiFactory>
{
    private readonly IndigoApiFactory _fabrica;

    public AutorizacionTests(IndigoApiFactory fabrica) => _fabrica = fabrica;

    [Fact]
    public async Task Vendedor_NoPuedeCrearProducto_Devuelve403()
    {
        var cliente = await _fabrica.ClienteVendedorAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/products",
            new CrearProductoRequest("Producto Que No Debe Existir", 10m, 5, CategoriaProducto.Otros),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Vendedor_NoPuedeActualizarProducto_Devuelve403()
    {
        var cliente = await _fabrica.ClienteVendedorAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/products/{Guid.NewGuid()}",
            new ActualizarProductoRequest("Nombre Nuevo", 20m, 1, CategoriaProducto.Hogar),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Vendedor_NoPuedeEliminarProducto_Devuelve403()
    {
        var cliente = await _fabrica.ClienteVendedorAsync();

        var respuesta = await cliente.DeleteAsync($"/api/products/{Guid.NewGuid()}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Vendedor_NoPuedeSubirImagen_Devuelve403()
    {
        var cliente = await _fabrica.ClienteVendedorAsync();

        using var contenido = new MultipartFormDataContent();
        using var archivo = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        contenido.Add(archivo, "file", "foto.png");

        var respuesta = await cliente.PostAsync($"/api/products/{Guid.NewGuid()}/imagen", contenido);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Vendedor_PuedeConsultarElCatalogo()
    {
        var cliente = await _fabrica.ClienteVendedorAsync();

        var listado = await cliente.GetAsync("/api/products");
        listado.StatusCode.Should().Be(HttpStatusCode.OK);

        var producto = await cliente.BuscarProductoAsync(DatosSembrados.ProductoConStock);

        var detalle = await cliente.GetAsync($"/api/products/{producto.Id}");
        detalle.StatusCode.Should().Be(HttpStatusCode.OK);

        var leido = await detalle.Content.ReadFromJsonAsync<ProductoDto>(OpcionesJson.Valor);
        leido!.Nombre.Should().Be(DatosSembrados.ProductoConStock);
    }

    [Fact]
    public async Task Vendedor_PuedeOperarSobreVentas()
    {
        var cliente = await _fabrica.ClienteVendedorAsync();

        var respuesta = await cliente.GetAsync("/api/sales");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
