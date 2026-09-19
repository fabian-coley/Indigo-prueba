using Indigo.Application.Abstractions.Security;

namespace Indigo.Application.Common;

internal static class CurrentUserExtensions
{
    public static string UsuarioIdRequerido(this ICurrentUserService usuarioActual) =>
        usuarioActual.UsuarioId
        ?? throw new InvalidOperationException("El request no tiene un usuario autenticado asociado.");
}
