using Indigo.Application.Common;
using Indigo.Application.Contracts.Auth;

namespace Indigo.Application.Mapping;

internal static class AuthMapping
{
    public static LoginResponse ToLoginResponse(
        this ResultadoToken token,
        string email,
        IReadOnlyList<string> roles) => new(token.Token, token.Expira, email, roles);
}
