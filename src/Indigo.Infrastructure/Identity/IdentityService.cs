using Indigo.Application.Abstractions.Security;
using Indigo.Application.Common;
using Indigo.Domain.Constants;
using Microsoft.AspNetCore.Identity;

namespace Indigo.Infrastructure.Identity;

public sealed class IdentityService : IIdentityService
{
    private const string CampoEmail = "email";
    private const string CampoPassword = "password";
    private const string CampoNombreCompleto = "nombreCompleto";

    private readonly UserManager<User> _usuarios;
    private readonly RoleManager<IdentityRole> _roles;

    public IdentityService(UserManager<User> usuarios, RoleManager<IdentityRole> roles)
    {
        ArgumentNullException.ThrowIfNull(usuarios);
        ArgumentNullException.ThrowIfNull(roles);

        _usuarios = usuarios;
        _roles = roles;
    }

    public async Task<ResultadoRegistro> RegistrarAsync(
        string email,
        string password,
        string nombreCompleto,
        string rol,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(rol);

        ct.ThrowIfCancellationRequested();

        if (!await _roles.RoleExistsAsync(rol))
        {
            throw new InvalidOperationException(
                $"El rol '{rol}' no existe. Los roles '{Roles.Admin}' y '{Roles.Vendedor}' los siembra el arranque.");
        }

        var usuario = new User
        {
            UserName = email,
            Email = email,
            NombreCompleto = nombreCompleto,
            EmailConfirmed = true,
        };

        var creacion = await _usuarios.CreateAsync(usuario, password);

        if (!creacion.Succeeded)
        {
            return ResultadoRegistro.Fallido(creacion.Errors.Select(Traducir));
        }

        var asignacion = await _usuarios.AddToRoleAsync(usuario, rol);

        if (!asignacion.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo asignar el rol '{rol}' al usuario '{email}'. {Describir(asignacion)}");
        }

        return ResultadoRegistro.Correcto(
            new UsuarioAutenticado(usuario.Id, email, nombreCompleto, [rol]));
    }

    public async Task<UsuarioAutenticado?> ValidarCredencialesAsync(
        string email,
        string password,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        ct.ThrowIfCancellationRequested();

        var usuario = await _usuarios.FindByEmailAsync(email);

        if (usuario is null)
        {
            return null;
        }

        if (!await _usuarios.CheckPasswordAsync(usuario, password))
        {
            return null;
        }

        var roles = await _usuarios.GetRolesAsync(usuario);

        return new UsuarioAutenticado(usuario.Id, usuario.Email ?? email, usuario.NombreCompleto, [.. roles]);
    }

    public async Task<string?> ObtenerEmailAsync(string usuarioId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioId);

        ct.ThrowIfCancellationRequested();

        var usuario = await _usuarios.FindByIdAsync(usuarioId);

        return usuario?.Email;
    }

    private static ErrorDeRegistro Traducir(IdentityError error) => new(CampoDe(error.Code), error.Description);

    private static string CampoDe(string codigo)
    {
        if (codigo.StartsWith("Password", StringComparison.Ordinal))
        {
            return CampoPassword;
        }

        if (codigo.StartsWith("Duplicate", StringComparison.Ordinal)
            || codigo is "InvalidEmail" or "InvalidUserName")
        {
            return CampoEmail;
        }

        return CampoNombreCompleto;
    }

    private static string Describir(IdentityResult resultado) =>
        string.Join("; ", resultado.Errors.Select(error => $"{error.Code}: {error.Description}"));
}
