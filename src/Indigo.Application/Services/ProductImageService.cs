using Indigo.Application.Abstractions.Persistence;
using Indigo.Application.Abstractions.Storage;
using Indigo.Application.Contracts.Productos;
using Indigo.Domain.Exceptions;

namespace Indigo.Application.Services;

public class ProductImageService
{
    public const long TamanoMaximoEnBytes = 5 * 1024 * 1024;

    private static readonly string[] ExtensionesPermitidas = [".jpg", ".jpeg", ".png", ".webp"];

    private readonly IProductRepository _productos;
    private readonly IBlobStorage _almacenamiento;
    private readonly IUnitOfWork _unitOfWork;

    public ProductImageService(
        IProductRepository productos,
        IBlobStorage almacenamiento,
        IUnitOfWork unitOfWork)
    {
        _productos = productos;
        _almacenamiento = almacenamiento;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductoImagenDto> SubirAsync(
        Guid productoId,
        Stream contenido,
        string nombreArchivo,
        long tamanoEnBytes,
        CancellationToken ct = default)
    {
        ValidarArchivo(nombreArchivo, tamanoEnBytes);

        var producto = await _productos.GetByIdAsync(productoId, ct: ct)
            ?? throw new ExcepcionNoEncontrado($"No existe el producto con id '{productoId}'.");

        var extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        var nombreGenerado = $"{Guid.NewGuid():N}{extension}";

        var url = await _almacenamiento.GuardarAsync(contenido, nombreGenerado, ct);
        var imagenAnterior = producto.ImagenUrl;

        try
        {
            producto.EstablecerImagen(url);
            _productos.Update(producto);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await _almacenamiento.EliminarAsync(url, ct);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(imagenAnterior))
        {
            await _almacenamiento.EliminarAsync(imagenAnterior, ct);
        }

        return new ProductoImagenDto(url);
    }

    public async Task EliminarAsync(Guid productoId, CancellationToken ct = default)
    {
        var producto = await _productos.GetByIdAsync(productoId, ct: ct)
            ?? throw new ExcepcionNoEncontrado($"No existe el producto con id '{productoId}'.");

        var imagen = producto.ImagenUrl;

        if (string.IsNullOrWhiteSpace(imagen))
        {
            return;
        }

        producto.EstablecerImagen(null);
        _productos.Update(producto);
        await _unitOfWork.SaveChangesAsync(ct);

        await _almacenamiento.EliminarAsync(imagen, ct);
    }

    private static void ValidarArchivo(string nombreArchivo, long tamanoEnBytes)
    {
        if (tamanoEnBytes <= 0)
        {
            throw new ExcepcionReglaDeNegocio("El archivo de imagen está vacío.");
        }

        if (tamanoEnBytes > TamanoMaximoEnBytes)
        {
            throw new ExcepcionReglaDeNegocio(
                $"La imagen no puede superar los {TamanoMaximoEnBytes / 1024 / 1024} MB.");
        }

        var extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();

        if (!ExtensionesPermitidas.Contains(extension))
        {
            throw new ExcepcionReglaDeNegocio(
                $"La extensión '{extension}' no está permitida. Se admiten: {string.Join(", ", ExtensionesPermitidas)}.");
        }
    }
}
