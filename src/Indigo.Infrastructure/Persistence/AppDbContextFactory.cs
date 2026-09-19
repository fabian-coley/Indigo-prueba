using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Indigo.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public const string NombreCadenaDeConexion = "Default";

    private const string NombreArchivoBaseDeDatos = "app.db";

    private const string NombreArchivoSolucion = "Indigo.slnx";

    public AppDbContext CreateDbContext(string[] args)
    {
        var cadenaDeConexion =
            Environment.GetEnvironmentVariable($"ConnectionStrings__{NombreCadenaDeConexion}")
            ?? CadenaDeConexionPorDefecto();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(cadenaDeConexion)
            .Options;

        return new AppDbContext(options);
    }

    private static string CadenaDeConexionPorDefecto() =>
        new SqliteConnectionStringBuilder
        {
            DataSource = RutaArchivoBaseDeDatos(),
        }.ToString();

    private static string RutaArchivoBaseDeDatos()
    {
        DirectoryInfo? directorio = new(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, NombreArchivoSolucion)))
            {
                return Path.Combine(directorio.FullName, NombreArchivoBaseDeDatos);
            }

            directorio = directorio.Parent;
        }

        return NombreArchivoBaseDeDatos;
    }
}
