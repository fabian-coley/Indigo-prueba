namespace Indigo.Application.Contracts.Auth;

public sealed record LoginResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    string Email,
    IReadOnlyList<string> Roles);
