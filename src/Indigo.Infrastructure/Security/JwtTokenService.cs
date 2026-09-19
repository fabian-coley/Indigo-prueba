using System.Text;
using Indigo.Application.Abstractions.Security;
using Indigo.Application.Common;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Indigo.Infrastructure.Security;

public sealed class JwtTokenService : ITokenService
{
    private const int BytesMinimosDeClave = 32;

    private const string ClaimRol = "role";

    private static readonly JsonWebTokenHandler Manejador = new();

    private readonly JwtOptions _opciones;
    private readonly TimeProvider _reloj;
    private readonly SigningCredentials _credenciales;

    public JwtTokenService(JwtOptions opciones, TimeProvider? reloj = null)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        var clave = Encoding.UTF8.GetBytes(opciones.Key ?? string.Empty);
        if (clave.Length < BytesMinimosDeClave)
            throw new InvalidOperationException(
                $"La configuración 'Jwt:Key' debe tener al menos {BytesMinimosDeClave} bytes para firmar con HS256.");

        if (string.IsNullOrWhiteSpace(opciones.Issuer))
            throw new InvalidOperationException("La configuración 'Jwt:Issuer' es obligatoria.");

        if (string.IsNullOrWhiteSpace(opciones.Audience))
            throw new InvalidOperationException("La configuración 'Jwt:Audience' es obligatoria.");

        if (opciones.ExpiryMinutes <= 0)
            throw new InvalidOperationException("La configuración 'Jwt:ExpiryMinutes' debe ser mayor que cero.");

        _opciones = opciones;
        _reloj = reloj ?? TimeProvider.System;
        _credenciales = new SigningCredentials(new SymmetricSecurityKey(clave), SecurityAlgorithms.HmacSha256);
    }

    public ResultadoToken GenerarToken(UsuarioAutenticado usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var emitidoEn = _reloj.GetUtcNow();
        var expira = emitidoEn.AddMinutes(_opciones.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _opciones.Issuer,
            Audience = _opciones.Audience,
            IssuedAt = emitidoEn.UtcDateTime,
            NotBefore = emitidoEn.UtcDateTime,
            Expires = expira.UtcDateTime,
            SigningCredentials = _credenciales,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = usuario.Id,
                [JwtRegisteredClaimNames.Email] = usuario.Email,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),

                [ClaimRol] = usuario.Roles,
            },
        };

        var token = Manejador.CreateToken(descriptor);
        return new ResultadoToken(token, expira);
    }
}
