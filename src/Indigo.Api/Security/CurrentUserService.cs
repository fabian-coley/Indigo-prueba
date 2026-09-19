using System.Security.Claims;
using Indigo.Application.Abstractions.Security;
using Microsoft.AspNetCore.Http;

using RolesDelDominio = Indigo.Domain.Constants.Roles;

namespace Indigo.Api.Security;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _acceso;

    public CurrentUserService(IHttpContextAccessor acceso)
    {
        ArgumentNullException.ThrowIfNull(acceso);
        _acceso = acceso;
    }

    public string? UsuarioId => Usuario?.FindFirst(ClaimsDelToken.UsuarioId)?.Value;

    public IReadOnlyList<string> Roles =>
        Usuario?.FindAll(ClaimsDelToken.Rol).Select(claim => claim.Value).ToArray() ?? [];

    public bool EstaAutenticado => Usuario?.Identity?.IsAuthenticated ?? false;

    public bool EsAdmin => Roles.Contains(RolesDelDominio.Admin, StringComparer.Ordinal);

    private ClaimsPrincipal? Usuario => _acceso.HttpContext?.User;
}
