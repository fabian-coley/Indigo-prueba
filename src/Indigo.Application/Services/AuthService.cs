using Indigo.Application.Abstractions.Security;
using Indigo.Application.Common;
using Indigo.Application.Contracts.Auth;
using Indigo.Application.Mapping;
using Indigo.Domain.Constants;

namespace Indigo.Application.Services;

public class AuthService
{
    private readonly IIdentityService _identity;
    private readonly ITokenService _tokenService;

    public AuthService(IIdentityService identity, ITokenService tokenService)
    {
        _identity = identity;
        _tokenService = tokenService;
    }

    public Task<ResultadoRegistro> RegistrarAsync(RegistroRequest request, CancellationToken ct = default) =>
        _identity.RegistrarAsync(request.Email, request.Password, request.NombreCompleto, Roles.Vendedor, ct);

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var usuario = await _identity.ValidarCredencialesAsync(request.Email, request.Password, ct);

        if (usuario is null)
        {
            return null;
        }

        var token = _tokenService.GenerarToken(usuario);

        return token.ToLoginResponse(usuario.Email, usuario.Roles);
    }
}
