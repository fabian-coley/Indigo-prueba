using Indigo.Application.Abstractions.Storage;
using Indigo.Infrastructure.Persistence;
using Indigo.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Indigo.Api.IntegrationTests.Infraestructura;

public sealed class IndigoApiFactory : WebApplicationFactory<Program>
{
    private readonly string _carpeta;
    private bool _eliminada;

    public IndigoApiFactory()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), "indigo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
    }

    public InMemoryBlobStorage Almacenamiento { get; } = new();

    public string RutaDeBaseDeDatos => Path.Combine(_carpeta, "indigo-tests.db");

    protected override void ConfigureWebHost(IWebHostBuilder constructor)
    {
        constructor.UseEnvironment("Development");

        constructor.ConfigureAppConfiguration((_, configuracion) =>
        {

            configuracion.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = "true",
            });
        });

        constructor.ConfigureServices(servicios =>
        {
            servicios.RemoveAll<IBlobStorage>();
            servicios.AddSingleton<IBlobStorage>(Almacenamiento);

            servicios.RemoveAll<DbContextOptions<AppDbContext>>();
            servicios.RemoveAll<DbContextOptions>();

            var cadena = new SqliteConnectionStringBuilder { DataSource = RutaDeBaseDeDatos }.ToString();

            servicios.AddDbContext<AppDbContext>(opciones => opciones.UseSqlite(cadena));
        });
    }

    public async Task<T> EnAlcanceAsync<T>(Func<IServiceProvider, Task<T>> accion)
    {
        ArgumentNullException.ThrowIfNull(accion);

        using var alcance = Services.CreateScope();

        return await accion(alcance.ServiceProvider);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing || _eliminada)
        {
            return;
        }

        _eliminada = true;

        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_carpeta, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
