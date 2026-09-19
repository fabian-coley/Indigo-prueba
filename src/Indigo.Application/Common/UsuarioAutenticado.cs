namespace Indigo.Application.Common;

public sealed record UsuarioAutenticado(
    string Id,
    string Email,
    string NombreCompleto,
    IReadOnlyList<string> Roles);
