using Indigo.Domain.Entities;
using Indigo.Domain.Enums;
using Indigo.Infrastructure.Identity;
using Indigo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Indigo.Infrastructure.Seed;

public sealed class DatabaseSeeder
{
    private const string RolAdmin = "Admin";
    private const string RolVendedor = "Vendedor";

    private const string EmailAdmin = "admin@indigo.com";
    private const string PasswordAdmin = "Admin123!";
    private const string NombreAdmin = "Administrador Indigo";

    private const string EmailVendedor = "vendedor@indigo.com";
    private const string PasswordVendedor = "Vendedor123!";
    private const string NombreVendedor = "Vendedor Indigo";

    private static readonly (string Nombre, decimal Precio, int Stock, CategoriaProducto Categoria)[] Catalogo =
    [
        ("Auriculares Bluetooth", 89.99m, 25, CategoriaProducto.Electrónica),
        ("Cafetera Express", 149.50m, 12, CategoriaProducto.Hogar),
        ("Cámara Digital", 320.00m, 8, CategoriaProducto.Electrónica),
        ("Detergente Líquido 1L", 4.20m, 60, CategoriaProducto.Hogar),
        ("Harina de Trigo 1kg", 1.95m, 0, CategoriaProducto.Alimentos),
        ("Lámpara de Escritorio", 45.00m, 15, CategoriaProducto.Hogar),
        ("Monitor LED 27 Pulgadas", 275.00m, 10, CategoriaProducto.Electrónica),
        ("Pilas AA Pack x4", 6.80m, 45, CategoriaProducto.Otros),
        ("Silla Ergonómica", 210.00m, 6, CategoriaProducto.Hogar),
        ("Teclado Mecánico", 95.00m, 30, CategoriaProducto.Electrónica),
        ("Yerba Mate 1kg", 8.75m, 50, CategoriaProducto.Alimentos),
        ("Zapatillas Running", 59.90m, 18, CategoriaProducto.Ropa),
    ];

    private readonly AppDbContext _contexto;
    private readonly UserManager<User> _usuarios;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        AppDbContext contexto,
        UserManager<User> usuarios,
        RoleManager<IdentityRole> roles,
        ILogger<DatabaseSeeder> logger)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(usuarios);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(logger);

        _contexto = contexto;
        _usuarios = usuarios;
        _roles = roles;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SembrarRolesAsync();
        await SembrarUsuariosAsync();
        await SembrarProductosAsync(ct);
    }

    private async Task SembrarRolesAsync()
    {
        foreach (var rol in new[] { RolAdmin, RolVendedor })
        {
            if (await _roles.RoleExistsAsync(rol))
            {
                continue;
            }

            var resultado = await _roles.CreateAsync(new IdentityRole(rol));
            Verificar(resultado, $"crear el rol '{rol}'");
        }
    }

    private async Task SembrarUsuariosAsync()
    {
        await SembrarUsuarioAsync(EmailAdmin, PasswordAdmin, NombreAdmin, RolAdmin);
        await SembrarUsuarioAsync(EmailVendedor, PasswordVendedor, NombreVendedor, RolVendedor);
    }

    private async Task SembrarUsuarioAsync(string email, string password, string nombreCompleto, string rol)
    {
        if (await _usuarios.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var usuario = new User
        {
            UserName = email,
            Email = email,
            NombreCompleto = nombreCompleto,
            EmailConfirmed = true,
        };

        var resultado = await _usuarios.CreateAsync(usuario, password);
        Verificar(resultado, $"crear el usuario '{email}'");

        var asignacion = await _usuarios.AddToRoleAsync(usuario, rol);
        Verificar(asignacion, $"asignar el rol '{rol}' al usuario '{email}'");

        _logger.LogInformation("Seed: usuario {Email} creado con rol {Rol}.", email, rol);
    }

    private async Task SembrarProductosAsync(CancellationToken ct)
    {
        if (await _contexto.Products.AnyAsync(ct))
        {
            return;
        }

        var productos = Catalogo
            .Select(fila => new Product(fila.Nombre, fila.Precio, fila.Stock, fila.Categoria))
            .ToList();

        _contexto.Products.AddRange(productos);
        await _contexto.SaveChangesAsync(ct);

        _logger.LogInformation("Seed: {Cantidad} productos creados.", productos.Count);
    }

    private static void Verificar(IdentityResult resultado, string accion)
    {
        if (resultado.Succeeded)
        {
            return;
        }

        var errores = string.Join("; ", resultado.Errors.Select(error => $"{error.Code}: {error.Description}"));

        throw new InvalidOperationException($"No se pudo {accion}. {errores}");
    }
}
