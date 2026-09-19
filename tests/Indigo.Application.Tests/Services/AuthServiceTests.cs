using FluentAssertions;
using Indigo.Application.Abstractions.Security;
using Indigo.Application.Common;
using Indigo.Application.Contracts.Auth;
using Indigo.Application.Services;
using Indigo.Domain.Constants;
using Moq;

namespace Indigo.Application.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IIdentityService> _identity = new();
    private readonly Mock<ITokenService> _tokenService = new();

    private AuthService CrearServicio() => new(_identity.Object, _tokenService.Object);

    [Fact]
    public async Task RegistrarAsync_DelegaEnIdentity_ConRolVendedor()
    {
        var request = new RegistroRequest("nuevo@indigo.com", "Password123!", "Nuevo Vendedor");
        var usuario = new UsuarioAutenticado("id-1", request.Email, request.NombreCompleto, [Roles.Vendedor]);

        _identity
            .Setup(i => i.RegistrarAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoRegistro.Correcto(usuario));

        var resultado = await CrearServicio().RegistrarAsync(request);

        resultado.Exitoso.Should().BeTrue();
        resultado.Usuario.Should().BeSameAs(usuario);
        resultado.Errores.Should().BeEmpty();

        _identity.Verify(
            i => i.RegistrarAsync(request.Email, request.Password, request.NombreCompleto, Roles.Vendedor, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RegistrarAsync_ConErroresDeIdentity_DevuelveElResultadoFallido()
    {
        var request = new RegistroRequest("repetido@indigo.com", "Password123!", "Repetido");
        ErrorDeRegistro[] errores = [new("email", "El email ya está registrado.")];

        _identity
            .Setup(i => i.RegistrarAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoRegistro.Fallido(errores));

        var resultado = await CrearServicio().RegistrarAsync(request);

        resultado.Exitoso.Should().BeFalse();
        resultado.Usuario.Should().BeNull();
        resultado.Errores.Should().BeEquivalentTo(errores);
    }

    [Fact]
    public async Task LoginAsync_ConCredencialesValidas_DevuelveElTokenConEmailYRoles()
    {
        var request = new LoginRequest("admin@indigo.com", "Admin123!");
        var expira = DateTimeOffset.UtcNow.AddHours(2);
        var usuario = new UsuarioAutenticado("admin-1", request.Email, "Admin Indigo", [Roles.Admin]);

        _identity.Setup(i => i.ValidarCredencialesAsync(request.Email, request.Password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usuario);
        _tokenService.Setup(t => t.GenerarToken(usuario)).Returns(new ResultadoToken("jwt-de-prueba", expira));

        var respuesta = await CrearServicio().LoginAsync(request);

        respuesta.Should().NotBeNull();
        respuesta!.Token.Should().Be("jwt-de-prueba");
        respuesta.ExpiresAt.Should().Be(expira);
        respuesta.Email.Should().Be(request.Email);
        respuesta.Roles.Should().Equal(Roles.Admin);
    }

    [Fact]
    public async Task LoginAsync_ConCredencialesInvalidas_DevuelveNullYSinToken()
    {
        var request = new LoginRequest("admin@indigo.com", "clave-incorrecta");

        _identity.Setup(i => i.ValidarCredencialesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UsuarioAutenticado?)null);

        var respuesta = await CrearServicio().LoginAsync(request);

        respuesta.Should().BeNull();

        _tokenService.Verify(t => t.GenerarToken(It.IsAny<UsuarioAutenticado>()), Times.Never);
    }
}
