using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Indigo.Api.IntegrationTests.Infraestructura;
using Indigo.Application.Common;
using Indigo.Application.Contracts.Productos;
using Indigo.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Indigo.Api.IntegrationTests;

public sealed class ProductosTests : IClassFixture<IndigoApiFactory>
{
    private readonly IndigoApiFactory _fabrica;

    public ProductosTests(IndigoApiFactory fabrica) => _fabrica = fabrica;


    [Fact]
    public async Task Crear_ConDatosValidos_Devuelve201ConLocationYProducto()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var nombre = $"Producto Nuevo {Guid.NewGuid():N}";

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/products",
            new CrearProductoRequest(nombre, 123.45m, 7, CategoriaProducto.Hogar),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var producto = await respuesta.Content.ReadFromJsonAsync<ProductoDto>(OpcionesJson.Valor);

        producto!.Id.Should().NotBeEmpty();
        producto.Nombre.Should().Be(nombre);
        producto.Precio.Should().Be(123.45m);
        producto.Stock.Should().Be(7);
        producto.Categoria.Should().Be(CategoriaProducto.Hogar);
        producto.ImagenUrl.Should().BeNull("el producto nace sin imagen");

        respuesta.Headers.Location.Should().NotBeNull();
        respuesta.Headers.Location!.AbsolutePath.Should().Be($"/api/products/{producto.Id}");

        var detalle = await cliente.GetAsync(respuesta.Headers.Location);

        detalle.StatusCode.Should().Be(HttpStatusCode.OK);

        var leido = await detalle.Content.ReadFromJsonAsync<ProductoDto>(OpcionesJson.Valor);
        leido.Should().BeEquivalentTo(producto);
    }

    [Fact]
    public async Task Crear_ConCuerpoVacio_Devuelve400ConErroresPorCampo()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.PostAsync(
            "/api/products",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ValidationProblemDetails>(OpcionesJson.Valor);

        problema!.Errors.Should().ContainKey("nombre");
        problema.Errors["nombre"].Should().Contain("El nombre del producto es obligatorio.");
        problema.Errors.Should().ContainKey("precio");
        problema.Errors["precio"].Should().Contain("El precio del producto debe ser mayor que cero.");
    }

    [Fact]
    public async Task Crear_ConNombreVacio_Devuelve400()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/products",
            new CrearProductoRequest("   ", 10m, 1, CategoriaProducto.Otros),
            OpcionesJson.Valor);

        var problema = await LeerProblemaDeValidacionAsync(respuesta, "nombre");

        problema.Errors["nombre"].Should().Contain("El nombre del producto es obligatorio.");
    }

    [Fact]
    public async Task Crear_ConNombreDemasiadoLargo_Devuelve400()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/products",
            new CrearProductoRequest(new string('a', 151), 10m, 1, CategoriaProducto.Otros),
            OpcionesJson.Valor);

        var problema = await LeerProblemaDeValidacionAsync(respuesta, "nombre");

        problema.Errors["nombre"].Should().Contain("El nombre del producto no puede superar los 150 caracteres.");
    }

    [Fact]
    public async Task Crear_ConPrecioNoPositivo_Devuelve400()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/products",
            new CrearProductoRequest("Producto Precio Cero", 0m, 1, CategoriaProducto.Otros),
            OpcionesJson.Valor);

        var problema = await LeerProblemaDeValidacionAsync(respuesta, "precio");

        problema.Errors["precio"].Should().Contain("El precio del producto debe ser mayor que cero.");
    }

    [Fact]
    public async Task Crear_ConStockNegativo_Devuelve400()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/products",
            new CrearProductoRequest("Producto Stock Negativo", 10m, -1, CategoriaProducto.Otros),
            OpcionesJson.Valor);

        var problema = await LeerProblemaDeValidacionAsync(respuesta, "stock");

        problema.Errors["stock"].Should().Contain("El stock del producto no puede ser negativo.");
    }

    [Fact]
    public async Task Crear_ConCategoriaFueraDeRango_Devuelve400()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.PostAsync(
            "/api/products",
            new StringContent(
                """{"nombre":"Producto Categoria Invalida","precio":10,"stock":1,"categoria":99}""",
                Encoding.UTF8,
                "application/json"));

        var problema = await LeerProblemaDeValidacionAsync(respuesta, "categoria");

        problema.Errors["categoria"].Should().Contain("La categoría '99' no es una categoría válida.");
    }


    [Fact]
    public async Task Actualizar_ConDatosValidos_DevuelveElProductoActualizado()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var producto = await CrearProductoAsync(cliente, CategoriaProducto.Otros);

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/products/{producto.Id}",
            new ActualizarProductoRequest($"{producto.Nombre} v2", 999.99m, 42, CategoriaProducto.Ropa),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var actualizado = await respuesta.Content.ReadFromJsonAsync<ProductoDto>(OpcionesJson.Valor);

        actualizado!.Id.Should().Be(producto.Id);
        actualizado.Nombre.Should().Be($"{producto.Nombre} v2");
        actualizado.Precio.Should().Be(999.99m);
        actualizado.Stock.Should().Be(42);
        actualizado.Categoria.Should().Be(CategoriaProducto.Ropa);
    }

    [Fact]
    public async Task Actualizar_NoTocaLaImagen()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var producto = await CrearProductoAsync(cliente, CategoriaProducto.Hogar);
        var imagenUrl = await SubirImagenAsync(cliente, producto.Id, "foto.png");

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/products/{producto.Id}",
            new ActualizarProductoRequest($"{producto.Nombre} v2", 15m, 3, CategoriaProducto.Hogar),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var actualizado = await respuesta.Content.ReadFromJsonAsync<ProductoDto>(OpcionesJson.Valor);

        actualizado!.ImagenUrl.Should().Be(imagenUrl);
        _fabrica.Almacenamiento.Existe(imagenUrl).Should().BeTrue();
    }

    [Fact]
    public async Task Actualizar_ConIdInexistente_Devuelve404()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var id = Guid.NewGuid();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/products/{id}",
            new ActualizarProductoRequest("No Existe", 10m, 1, CategoriaProducto.Otros),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be($"No existe el producto con id '{id}'.");
    }

    [Fact]
    public async Task ObtenerPorId_ConIdInexistente_Devuelve404()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var id = Guid.NewGuid();

        var respuesta = await cliente.GetAsync($"/api/products/{id}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be($"No existe un producto activo con id {id}.");
    }

    [Fact]
    public async Task Eliminar_DesactivaElProductoYLiberaSuImagen()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var producto = await CrearProductoAsync(cliente, CategoriaProducto.Otros);
        var imagenUrl = await SubirImagenAsync(cliente, producto.Id, "foto.png");

        var baja = await cliente.DeleteAsync($"/api/products/{producto.Id}");

        baja.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detalle = await cliente.GetAsync($"/api/products/{producto.Id}");
        detalle.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var segundaBaja = await cliente.DeleteAsync($"/api/products/{producto.Id}");
        segundaBaja.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _fabrica.Almacenamiento.Existe(imagenUrl).Should().BeFalse("la baja limpia el archivo de la imagen");
    }


    [Fact]
    public async Task SubirImagen_GuardaElArchivoYDevuelveLaUrlRelativa()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var producto = await CrearProductoAsync(cliente, CategoriaProducto.Hogar);

        var imagenUrl = await SubirImagenAsync(cliente, producto.Id, "foto.png");

        imagenUrl.Should().StartWith("/uploads/");
        imagenUrl.Should().EndWith(".png");
        _fabrica.Almacenamiento.Existe(imagenUrl).Should().BeTrue();

        var detalle = await cliente.GetAsync($"/api/products/{producto.Id}");
        var leido = await detalle.Content.ReadFromJsonAsync<ProductoDto>(OpcionesJson.Valor);

        leido!.ImagenUrl.Should().Be(imagenUrl, "la URL queda persistida en el producto");
    }

    [Fact]
    public async Task SubirImagen_ReemplazaLaAnteriorYBorraElArchivoViejo()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var producto = await CrearProductoAsync(cliente, CategoriaProducto.Hogar);

        var primera = await SubirImagenAsync(cliente, producto.Id, "foto.png");
        var segunda = await SubirImagenAsync(cliente, producto.Id, "foto-2.png");

        segunda.Should().NotBe(primera);
        _fabrica.Almacenamiento.Existe(primera).Should().BeFalse("reemplazar la imagen borra la anterior");
        _fabrica.Almacenamiento.Existe(segunda).Should().BeTrue();

        var detalle = await cliente.GetAsync($"/api/products/{producto.Id}");
        var leido = await detalle.Content.ReadFromJsonAsync<ProductoDto>(OpcionesJson.Valor);

        leido!.ImagenUrl.Should().Be(segunda);
    }

    [Fact]
    public async Task SubirImagen_ConExtensionNoPermitida_Devuelve400()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var producto = await CrearProductoAsync(cliente, CategoriaProducto.Hogar);

        using var contenido = new MultipartFormDataContent();
        using var archivo = new ByteArrayContent([1, 2, 3]);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        contenido.Add(archivo, "file", "notas.txt");

        var respuesta = await cliente.PostAsync($"/api/products/{producto.Id}/imagen", contenido);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be("La extensión '.txt' no está permitida. Se admiten: .jpg, .jpeg, .png, .webp.");
    }

    [Fact]
    public async Task SubirImagen_SinArchivo_Devuelve400()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var producto = await CrearProductoAsync(cliente, CategoriaProducto.Hogar);

        using var contenido = new MultipartFormDataContent();
        contenido.Add(new StringContent("sin-archivo"), "descripcion");

        var respuesta = await cliente.PostAsync($"/api/products/{producto.Id}/imagen", contenido);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await LeerProblemaDeValidacionAsync(respuesta, "file");

        problema.Errors["file"].Should().Contain("El archivo es obligatorio.");
    }

    [Fact]
    public async Task SubirImagen_ConProductoInexistente_Devuelve404()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var id = Guid.NewGuid();

        using var contenido = new MultipartFormDataContent();
        using var archivo = new ByteArrayContent([1, 2, 3]);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        contenido.Add(archivo, "file", "foto.png");

        var respuesta = await cliente.PostAsync($"/api/products/{id}/imagen", contenido);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be($"No existe el producto con id '{id}'.");
    }


    [Fact]
    public async Task Listar_FiltraPorCategoria()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var producto = await CrearProductoAsync(cliente, CategoriaProducto.Ropa);

        var respuesta = await cliente.GetAsync("/api/products?pageSize=100&categoria=Ropa");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagina = await respuesta.Content.ReadFromJsonAsync<PagedResult<ProductoDto>>(OpcionesJson.Valor);

        pagina!.Items.Should().OnlyContain(candidato => candidato.Categoria == CategoriaProducto.Ropa);
        pagina.Items.Select(candidato => candidato.Nombre).Should().Contain(producto.Nombre);
        pagina.Items.Select(candidato => candidato.Nombre).Should().Contain("Zapatillas Running");
    }

    [Fact]
    public async Task Listar_FiltraPorSearch()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var marca = Guid.NewGuid().ToString("N");
        var producto = await CrearProductoAsync(cliente, CategoriaProducto.Otros, sufijo: marca);

        var respuesta = await cliente.GetAsync($"/api/products?pageSize=100&search={marca}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagina = await respuesta.Content.ReadFromJsonAsync<PagedResult<ProductoDto>>(OpcionesJson.Valor);

        pagina!.Items.Should().ContainSingle();
        pagina.Items[0].Id.Should().Be(producto.Id);
    }

    [Fact]
    public async Task Listar_NoDevuelveProductosDadosDeBaja()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var producto = await CrearProductoAsync(cliente, CategoriaProducto.Otros);

        (await cliente.DeleteAsync($"/api/products/{producto.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var respuesta = await cliente.GetAsync($"/api/products?pageSize=100&search={Uri.EscapeDataString(producto.Nombre)}");
        var pagina = await respuesta.Content.ReadFromJsonAsync<PagedResult<ProductoDto>>(OpcionesJson.Valor);

        pagina!.Items.Should().BeEmpty();
    }


    private static async Task<ProductoDto> CrearProductoAsync(
        HttpClient cliente,
        CategoriaProducto categoria,
        string? sufijo = null)
    {
        var nombre = $"Producto {sufijo ?? Guid.NewGuid().ToString("N")}";

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/products",
            new CrearProductoRequest(nombre, 50m, 10, categoria),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await respuesta.Content.ReadFromJsonAsync<ProductoDto>(OpcionesJson.Valor))!;
    }

    private static async Task<string> SubirImagenAsync(HttpClient cliente, Guid productoId, string nombreArchivo)
    {
        using var contenido = new MultipartFormDataContent();
        using var archivo = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        contenido.Add(archivo, "file", nombreArchivo);

        var respuesta = await cliente.PostAsync($"/api/products/{productoId}/imagen", contenido);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var imagen = await respuesta.Content.ReadFromJsonAsync<ProductoImagenDto>(OpcionesJson.Valor);

        return imagen!.ImagenUrl;
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
