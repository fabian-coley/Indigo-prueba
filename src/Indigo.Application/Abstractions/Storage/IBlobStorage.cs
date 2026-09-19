namespace Indigo.Application.Abstractions.Storage;

public interface IBlobStorage
{
    Task<string> GuardarAsync(Stream contenido, string nombreArchivo, CancellationToken ct = default);

    Task EliminarAsync(string url, CancellationToken ct = default);
}
