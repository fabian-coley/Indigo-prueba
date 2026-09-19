using Indigo.Application.Abstractions.Storage;

namespace Indigo.Infrastructure.Storage;

public sealed class LocalFileStorage : IBlobStorage
{
    private readonly string _rutaBase;
    private readonly string _rutaPublica;

    public LocalFileStorage(string rutaBase, string rutaPublica = "/uploads")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaBase);
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaPublica);

        _rutaBase = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rutaBase));
        _rutaPublica = rutaPublica == "/" ? string.Empty : "/" + rutaPublica.Trim('/');
    }

    public async Task<string> GuardarAsync(Stream contenido, string nombreArchivo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        if (!TryResolverRuta(nombreArchivo, out var destino))
        {
            throw new ArgumentException(
                $"Nombre de archivo inválido para el almacenamiento: '{nombreArchivo}'.", nameof(nombreArchivo));
        }

        Directory.CreateDirectory(_rutaBase);

        await using (var salida = new FileStream(destino, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await contenido.CopyToAsync(salida, ct);
        }

        return $"{_rutaPublica}/{Path.GetFileName(destino)}";
    }

    public Task EliminarAsync(string url, CancellationToken ct = default)
    {
        if (TryResolverRuta(Path.GetFileName(url), out var ruta))
        {
            try
            {
                File.Delete(ruta);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return Task.CompletedTask;
    }

    private bool TryResolverRuta(string? nombreArchivo, out string ruta)
    {
        ruta = string.Empty;

        if (string.IsNullOrWhiteSpace(nombreArchivo) || Path.GetFileName(nombreArchivo) != nombreArchivo)
            return false;

        var candidata = Path.GetFullPath(Path.Combine(_rutaBase, nombreArchivo));

        if (!candidata.StartsWith(_rutaBase + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return false;

        ruta = candidata;
        return true;
    }
}
