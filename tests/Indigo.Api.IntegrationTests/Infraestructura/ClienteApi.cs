using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Indigo.Application.Common;
using Indigo.Application.Contracts.Auth;
using Indigo.Application.Contracts.Productos;
using Indigo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Indigo.Api.IntegrationTests.Infraestructura;

internal static class OpcionesJson
{
    public static readonly JsonSerializerOptions Valor = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}

internal static class DatosSembrados
{
    public const string EmailAdmin = "admin@indigo.com";
    public const string PasswordAdmin = "Admin123!";
    public const string EmailVendedor = "vendedor@indigo.com";
    public const string PasswordVendedor = "Vendedor123!";

    public const string PasswordDePrueba = "Vendedor123!";

    public const string ProductoConStock = "Monitor LED 27 Pulgadas";

    public const string ProductoSinStock = "Harina de Trigo 1kg";

    public const string ProductoAbundante = "Yerba Mate 1kg";
}

internal static class ClienteApi
{
    public static async Task<string> IniciarSesionAsync(this HttpClient cliente, string email, string password)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, "el login de {0} debería responder 200", email);

        var sesion = await respuesta.Content.ReadFromJsonAsync<LoginResponse>(OpcionesJson.Valor);

        return sesion!.Token;
    }

    public static async Task<HttpClient> ClienteAutenticadoAsync(this IndigoApiFactory fabrica, string email, string password)
    {
        var cliente = fabrica.CreateClient();
        var token = await cliente.IniciarSesionAsync(email, password);

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return cliente;
    }

    public static Task<HttpClient> ClienteAdminAsync(this IndigoApiFactory fabrica) =>
        fabrica.ClienteAutenticadoAsync(DatosSembrados.EmailAdmin, DatosSembrados.PasswordAdmin);

    public static Task<HttpClient> ClienteVendedorAsync(this IndigoApiFactory fabrica) =>
        fabrica.ClienteAutenticadoAsync(DatosSembrados.EmailVendedor, DatosSembrados.PasswordVendedor);

    public static async Task<(HttpClient Cliente, string Email)> ClienteVendedorNuevoAsync(
        this IndigoApiFactory fabrica)
    {
        var email = $"vendedor-{Guid.NewGuid():N}@indigo.com";

        var cliente = fabrica.CreateClient();

        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, DatosSembrados.PasswordDePrueba, "Vendedor De Prueba"),
            OpcionesJson.Valor);

        registro.StatusCode.Should().Be(HttpStatusCode.OK, "el registro de {0} debería responder 200", email);

        var token = await cliente.IniciarSesionAsync(email, DatosSembrados.PasswordDePrueba);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (cliente, email);
    }

    public static async Task<ProductoDto> BuscarProductoAsync(this HttpClient cliente, string nombre)
    {
        var respuesta = await cliente.GetAsync($"/api/products?pageSize=100&search={Uri.EscapeDataString(nombre)}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagina = await respuesta.Content.ReadFromJsonAsync<PagedResult<ProductoDto>>(OpcionesJson.Valor);

        var coincidencias = pagina!.Items.Where(producto => producto.Nombre == nombre).ToList();
        coincidencias.Should().ContainSingle("el seed debe tener un único producto '{0}'", nombre);

        return coincidencias[0];
    }

    public static Task<string> BuscarUsuarioIdAsync(this IndigoApiFactory fabrica, string email) =>
        fabrica.EnAlcanceAsync(async servicios =>
        {
            var contexto = servicios.GetRequiredService<AppDbContext>();

            var usuario = await contexto.Users.SingleAsync(candidato => candidato.Email == email);

            return usuario.Id;
        });
}
