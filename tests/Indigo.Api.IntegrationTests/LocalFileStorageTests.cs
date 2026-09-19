using FluentAssertions;
using Indigo.Infrastructure.Storage;

namespace Indigo.Api.IntegrationTests;

public sealed class LocalFileStorageTests : IDisposable
{
    private const string UrlPorDefecto = "/uploads/foto.png";

    private readonly string _raiz;
    private readonly string _carpeta;
    private readonly LocalFileStorage _almacenamiento;

    public LocalFileStorageTests()
    {
        _raiz = Path.Combine(Path.GetTempPath(), "indigo-storage-tests", Guid.NewGuid().ToString("N"));
        _carpeta = Path.Combine(_raiz, "uploads");

        _almacenamiento = new LocalFileStorage(_carpeta);
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
            Directory.Delete(_raiz, recursive: true);
    }


    [Fact]
    public async Task Guardar_DevuelveLaUrlRelativaYEscribeElArchivo()
    {
        var url = await _almacenamiento.GuardarAsync(Flujo(0x89, 0x50, 0x4E, 0x47), "foto.png");

        url.Should().Be(UrlPorDefecto);

        url.Should().StartWith("/");
        url.Should().NotContain(_carpeta);

        File.Exists(RutaEnLaCarpeta("foto.png")).Should().BeTrue();
        (await File.ReadAllBytesAsync(RutaEnLaCarpeta("foto.png"))).Should().Equal(0x89, 0x50, 0x4E, 0x47);
    }

    [Fact]
    public async Task Guardar_CreaLaCarpetaBaseAlVuelo()
    {
        Directory.Exists(_carpeta).Should().BeFalse();

        await _almacenamiento.GuardarAsync(Flujo(1), "foto.png");

        Directory.Exists(_carpeta).Should().BeTrue();
    }

    [Fact]
    public async Task Guardar_ConElMismoNombre_ReemplazaElContenido()
    {
        var primera = await _almacenamiento.GuardarAsync(Flujo(1, 2, 3), "foto.png");
        var segunda = await _almacenamiento.GuardarAsync(Flujo(9, 9, 9, 9, 9), "foto.png");

        segunda.Should().Be(primera, "la URL depende del nombre, no del contenido");

        (await File.ReadAllBytesAsync(RutaEnLaCarpeta("foto.png"))).Should().Equal(9, 9, 9, 9, 9);
        Directory.GetFiles(_carpeta).Should().ContainSingle("reemplazar no deja archivos huérfanos");
    }

    [Theory]
    [InlineData("/media", "/media/foto.png")]
    [InlineData("media", "/media/foto.png")]
    [InlineData("/uploads/", "/uploads/foto.png")]
    [InlineData("/", "/foto.png")]
    public async Task Guardar_ConRutaPublicaPersonalizada_NormalizaElPrefijo(string rutaPublica, string esperada)
    {
        var almacenamiento = new LocalFileStorage(_carpeta, rutaPublica);

        var url = await almacenamiento.GuardarAsync(Flujo(1), "foto.png");

        url.Should().Be(esperada);
    }

    [Fact]
    public async Task Guardar_ConStreamNulo_Lanza()
    {
        var accion = async () => await _almacenamiento.GuardarAsync(null!, "foto.png");

        await accion.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("../foto.png")]
    [InlineData("sub/foto.png")]
    [InlineData("/etc/passwd")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Guardar_ConNombreInvalido_LanzaYNoEscribeNada(string nombreArchivo)
    {
        var accion = async () => await _almacenamiento.GuardarAsync(Flujo(1), nombreArchivo);

        var excepcion = await accion.Should().ThrowAsync<ArgumentException>();

        excepcion.Which.ParamName.Should().Be("nombreArchivo");
        excepcion.Which.Message.Should().StartWith(
            $"Nombre de archivo inválido para el almacenamiento: '{nombreArchivo}'.");

        Directory.Exists(_carpeta).Should().BeFalse("el nombre se valida antes de tocar el disco");
    }


    [Fact]
    public async Task Eliminar_BorraElArchivoYEsIdempotente()
    {
        await _almacenamiento.GuardarAsync(Flujo(1), "foto.png");

        await _almacenamiento.EliminarAsync(UrlPorDefecto);

        File.Exists(RutaEnLaCarpeta("foto.png")).Should().BeFalse();

        var repetir = async () => await _almacenamiento.EliminarAsync(UrlPorDefecto);

        await repetir.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-una-url")]
    [InlineData("/uploads/no-existe.png")]
    public async Task Eliminar_ConUrlQueNoResuelve_NoLanza(string url)
    {
        var accion = async () => await _almacenamiento.EliminarAsync(url);

        await accion.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Eliminar_ConUrlManipulada_NoTocaLosArchivosDeAfuera()
    {
        await _almacenamiento.GuardarAsync(Flujo(1), "propio.png");

        var hermanoMismoNombre = Path.Combine(_raiz, "otro.png");
        var hermanoOtroNombre = Path.Combine(_raiz, "intacto.png");

        await File.WriteAllBytesAsync(hermanoMismoNombre, [7, 7, 7]);
        await File.WriteAllBytesAsync(hermanoOtroNombre, [8, 8, 8]);

        await _almacenamiento.EliminarAsync("/uploads/../otro.png");
        await _almacenamiento.EliminarAsync(hermanoMismoNombre);
        await _almacenamiento.EliminarAsync(hermanoOtroNombre);

        File.Exists(hermanoMismoNombre).Should().BeTrue("nadie borra fuera de la carpeta base");
        File.Exists(hermanoOtroNombre).Should().BeTrue();
        File.Exists(RutaEnLaCarpeta("propio.png")).Should().BeTrue("el propio sigue ahí: no se borró nada");
    }


    private string RutaEnLaCarpeta(string nombreArchivo) => Path.Combine(_carpeta, nombreArchivo);

    private static MemoryStream Flujo(params byte[] bytes) => new(bytes);
}
