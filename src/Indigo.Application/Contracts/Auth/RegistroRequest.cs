namespace Indigo.Application.Contracts.Auth;

public sealed record RegistroRequest(string Email, string Password, string NombreCompleto);
