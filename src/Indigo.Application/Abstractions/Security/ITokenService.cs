using Indigo.Application.Common;

namespace Indigo.Application.Abstractions.Security;

public interface ITokenService
{
    ResultadoToken GenerarToken(UsuarioAutenticado usuario);
}
