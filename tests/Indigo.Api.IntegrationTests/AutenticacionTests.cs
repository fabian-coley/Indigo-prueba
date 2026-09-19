using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Indigo.Api.IntegrationTests.Infraestructura;
using Indigo.Application.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Indigo.Api.IntegrationTests;

public sealed class AutenticacionTests : IClassFixture<IndigoApiFactory>
{
    private readonly IndigoApiFactory _fabrica;

    public AutenticacionTests(IndigoApiFactory fabrica) => _fabrica = fabrica;

    [Fact]
    public async Task Login_ConCredencialesSembradas_DevuelveTokenConRolYExpiracion()
    {
        var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(DatosSembrados.EmailAdmin, DatosSembrados.PasswordAdmin),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var sesion = await respuesta.Content.ReadFromJsonAsync<LoginResponse>(OpcionesJson.Valor);

        sesion!.Token.Should().NotBeNullOrWhiteSpace();
        sesion.Email.Should().Be(DatosSembrados.EmailAdmin);
        sesion.Roles.Should().Contain("Admin");
        sesion.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow, "el token se emite con una hora de vigencia");
    }

    [Fact]
    public async Task Login_ConPasswordIncorrecta_Devuelve401ConMensajeGenerico()
    {
        var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(DatosSembrados.EmailAdmin, "no-es-la-clave"),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Contain("El email o la contraseña no son correctos.");
    }

    [Fact]
    public async Task Login_ConEmailInexistente_Devuelve401ConElMismoMensaje()
    {
        var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest($"nadie-{Guid.NewGuid():N}@indigo.com", DatosSembrados.PasswordAdmin),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Contain("El email o la contraseña no son correctos.");
    }

    [Fact]
    public async Task Login_ConCuerpoVacio_Devuelve400ConErrorPorCampo()
    {
        var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.PostAsync(
            "/api/auth/login",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ValidationProblemDetails>(OpcionesJson.Valor);

        problema!.Errors.Should().ContainKey("email");
        problema.Errors.Should().ContainKey("password");
    }

    [Fact]
    public async Task Registro_ConEmailNuevo_CreaUnVendedorQuePuedeEntrar()
    {
        var cliente = _fabrica.CreateClient();
        var email = $"nuevo-{Guid.NewGuid():N}@indigo.com";

        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, DatosSembrados.PasswordDePrueba, "Vendedor Nuevo"),
            OpcionesJson.Valor);

        registro.StatusCode.Should().Be(HttpStatusCode.OK);

        var sesion = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, DatosSembrados.PasswordDePrueba),
            OpcionesJson.Valor);

        sesion.StatusCode.Should().Be(HttpStatusCode.OK);

        var respuesta = await sesion.Content.ReadFromJsonAsync<LoginResponse>(OpcionesJson.Valor);

        respuesta!.Roles.Should().BeEquivalentTo(new[] { "Vendedor" });
    }

    [Fact]
    public async Task Registro_ConEmailRepetido_Devuelve400()
    {
        var cliente = _fabrica.CreateClient();
        var email = $"repetido-{Guid.NewGuid():N}@indigo.com";

        var peticion = new RegistroRequest(email, DatosSembrados.PasswordDePrueba, "Vendedor Repetido");

        var primero = await cliente.PostAsJsonAsync("/api/auth/register", peticion, OpcionesJson.Valor);
        primero.StatusCode.Should().Be(HttpStatusCode.OK);

        var segundo = await cliente.PostAsJsonAsync("/api/auth/register", peticion, OpcionesJson.Valor);

        segundo.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await segundo.Content.ReadFromJsonAsync<ValidationProblemDetails>(OpcionesJson.Valor);

        problema!.Errors.Should().ContainKey("email");
    }

    [Fact]
    public async Task Registro_ConEmailMalFormado_Devuelve400()
    {
        var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest("no-es-un-email", DatosSembrados.PasswordDePrueba, "Vendedor Mal Formado"),
            OpcionesJson.Valor);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ValidationProblemDetails>(OpcionesJson.Valor);

        problema!.Errors.Should().ContainKey("email");
    }

    [Fact]
    public async Task Request_SinToken_Devuelve401()
    {
        var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/api/products");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_ConTokenMalFormado_Devuelve401()
    {
        var cliente = _fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "esto-no-es-un-jwt");

        var respuesta = await cliente.GetAsync("/api/products");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
