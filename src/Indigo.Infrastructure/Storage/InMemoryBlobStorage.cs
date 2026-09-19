using System.Collections.Concurrent;
using Indigo.Application.Abstractions.Storage;

namespace Indigo.Infrastructure.Storage;

public sealed class InMemoryBlobStorage : IBlobStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _archivos = new(StringComparer.Ordinal);

    public async Task<string> GuardarAsync(Stream contenido, string nombreArchivo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        using var buffer = new MemoryStream();
        await contenido.CopyToAsync(buffer, ct);

        var url = $"/uploads/{nombreArchivo}";
        _archivos[url] = buffer.ToArray();
        return url;
    }

    public Task EliminarAsync(string url, CancellationToken ct = default)
    {
        _archivos.TryRemove(url, out _);
        return Task.CompletedTask;
    }

    public bool Existe(string url) => _archivos.ContainsKey(url);

    public byte[]? ObtenerContenido(string url) => _archivos.TryGetValue(url, out var bytes) ? bytes : null;

    public void Limpiar() => _archivos.Clear();
}
