namespace Indigo.Application.Abstractions.Security;

public interface ICurrentUserService
{
    string? UsuarioId { get; }

    IReadOnlyList<string> Roles { get; }

    bool EstaAutenticado { get; }

    bool EsAdmin { get; }
}
