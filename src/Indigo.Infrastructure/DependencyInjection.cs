using Indigo.Application.Abstractions.Persistence;
using Indigo.Application.Abstractions.Security;
using Indigo.Application.Abstractions.Storage;
using Indigo.Application.Services;
using Indigo.Infrastructure.Identity;
using Indigo.Infrastructure.Persistence;
using Indigo.Infrastructure.Persistence.Repositories;
using Indigo.Infrastructure.Security;
using Indigo.Infrastructure.Seed;
using Indigo.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Indigo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection servicios,
        string cadenaDeConexion,
        JwtOptions opcionesJwt,
        string rutaDeAlmacenamiento)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentException.ThrowIfNullOrWhiteSpace(cadenaDeConexion);
        ArgumentNullException.ThrowIfNull(opcionesJwt);
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaDeAlmacenamiento);

        servicios.AddDbContext<AppDbContext>(opciones => opciones.UseSqlite(cadenaDeConexion));

        servicios
            .AddIdentityCore<User>(opciones => opciones.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        servicios.AddScoped<IProductRepository, ProductRepository>();
        servicios.AddScoped<ISaleRepository, SaleRepository>();
        servicios.AddScoped<IUnitOfWork, UnitOfWork>();
        servicios.AddScoped<IIdentityService, IdentityService>();

        servicios.AddSingleton<ITokenService>(new JwtTokenService(opcionesJwt));

        servicios.AddSingleton<IBlobStorage>(new LocalFileStorage(rutaDeAlmacenamiento));

        servicios.AddScoped<AuthService>();
        servicios.AddScoped<ProductService>();
        servicios.AddScoped<ProductImageService>();
        servicios.AddScoped<SaleService>();
        servicios.AddScoped<SalesReportService>();

        servicios.AddScoped<DatabaseSeeder>();

        return servicios;
    }
}
