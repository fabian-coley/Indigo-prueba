using Indigo.Application.Common;

namespace Indigo.Application.Abstractions.Security;

public interface IIdentityService
{
    Task<ResultadoRegistro> RegistrarAsync(
        string email,
        string password,
        string nombreCompleto,
        string rol,
        CancellationToken ct = default);

    Task<UsuarioAutenticado?> ValidarCredencialesAsync(string email, string password, CancellationToken ct = default);

    Task<string?> ObtenerEmailAsync(string usuarioId, CancellationToken ct = default);
}
