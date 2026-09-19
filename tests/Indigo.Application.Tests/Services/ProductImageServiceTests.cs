using FluentAssertions;
using Indigo.Application.Abstractions.Persistence;
using Indigo.Application.Abstractions.Storage;
using Indigo.Application.Services;
using Indigo.Domain.Entities;
using Indigo.Domain.Enums;
using Indigo.Domain.Exceptions;
using Moq;

namespace Indigo.Application.Tests.Services;

public class ProductImageServiceTests
{
    private readonly Mock<IProductRepository> _productos = new();
    private readonly Mock<IBlobStorage> _almacenamiento = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private ProductImageService CrearServicio() => new(_productos.Object, _almacenamiento.Object, _unitOfWork.Object);

    private static Product Producto() => new("Teclado mecánico", 49.99m, 10, CategoriaProducto.Electrónica);

    private void PrepararProducto(Product producto) =>
        _productos.Setup(p => p.GetByIdAsync(producto.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(producto);

    [Theory]
    [InlineData("foto.jpg", 0)]
    [InlineData("foto.jpg", -1)]
    [InlineData("foto.jpg", ProductImageService.TamanoMaximoEnBytes + 1)]
    [InlineData("documento.pdf", 1024)]
    [InlineData("script.exe", 1024)]
    [InlineData("animacion.gif", 1024)]
    [InlineData("sin-extension", 1024)]
    public async Task SubirAsync_ConArchivoInvalido_RechazaAntesDeTocarLaBase(string nombreArchivo, long tamano)
    {
        var act = () => CrearServicio().SubirAsync(Guid.NewGuid(), Stream.Null, nombreArchivo, tamano);

        await act.Should().ThrowAsync<ExcepcionReglaDeNegocio>();

        _productos.Verify(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _almacenamiento.Verify(a => a.GuardarAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("foto.jpg", ".jpg")]
    [InlineData("foto.JPG", ".jpg")]
    [InlineData("foto.jpeg", ".jpeg")]
    [InlineData("foto.PNG", ".png")]
    [InlineData("foto.webp", ".webp")]
    public async Task SubirAsync_ConExtensionPermitida_GuardaConNombreGeneradoPorElServidor(string nombreArchivo, string extensionEsperada)
    {
        var producto = Producto();
        PrepararProducto(producto);
        string? nombreUsado = null;

        _almacenamiento
            .Setup(a => a.GuardarAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, string, CancellationToken>((_, nombre, _) => nombreUsado = nombre)
            .ReturnsAsync("/uploads/generado.png");

        var resultado = await CrearServicio().SubirAsync(producto.Id, Stream.Null, nombreArchivo, 1024);

        nombreUsado.Should().NotBeNull();
        nombreUsado.Should().NotBe(nombreArchivo);
        nombreUsado.Should().EndWith(extensionEsperada);
        Guid.TryParseExact(nombreUsado![..^extensionEsperada.Length], "N", out _).Should().BeTrue();

        resultado.ImagenUrl.Should().Be("/uploads/generado.png");
        producto.ImagenUrl.Should().Be("/uploads/generado.png");
    }

    [Fact]
    public async Task SubirAsync_ConImagenPrevia_EliminaLaAnteriorDespuesDePersistirLaNueva()
    {
        var producto = Producto();
        producto.EstablecerImagen("/uploads/vieja.png");
        PrepararProducto(producto);

        var secuencia = new MockSequence();
        _almacenamiento.InSequence(secuencia)
            .Setup(a => a.GuardarAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/uploads/nueva.png");
        _unitOfWork.InSequence(secuencia)
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _almacenamiento.InSequence(secuencia)
            .Setup(a => a.EliminarAsync("/uploads/vieja.png", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var resultado = await CrearServicio().SubirAsync(producto.Id, Stream.Null, "foto.png", 1024);

        resultado.ImagenUrl.Should().Be("/uploads/nueva.png");
        producto.ImagenUrl.Should().Be("/uploads/nueva.png");
        _almacenamiento.Verify(a => a.EliminarAsync("/uploads/vieja.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubirAsync_SinImagenPrevia_NoEliminaNada()
    {
        var producto = Producto();
        PrepararProducto(producto);

        _almacenamiento
            .Setup(a => a.GuardarAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/uploads/nueva.png");

        await CrearServicio().SubirAsync(producto.Id, Stream.Null, "foto.png", 1024);

        _almacenamiento.Verify(a => a.EliminarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubirAsync_CuandoFallaLaPersistencia_EliminaElArchivoRecienGuardado()
    {
        var producto = Producto();
        PrepararProducto(producto);

        _almacenamiento
            .Setup(a => a.GuardarAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/uploads/huerfano.png");
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("falló la base"));

        var act = () => CrearServicio().SubirAsync(producto.Id, Stream.Null, "foto.png", 1024);

        await act.Should().ThrowAsync<InvalidOperationException>();

        _almacenamiento.Verify(a => a.EliminarAsync("/uploads/huerfano.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubirAsync_CuandoElProductoNoExiste_LanzaExcepcionNoEncontrado()
    {
        var id = Guid.NewGuid();
        _productos.Setup(p => p.GetByIdAsync(id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var act = () => CrearServicio().SubirAsync(id, Stream.Null, "foto.png", 1024);

        await act.Should().ThrowAsync<ExcepcionNoEncontrado>();
        _almacenamiento.Verify(a => a.GuardarAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EliminarAsync_ConImagen_LimpiaLaUrlYPersisteAntesDeBorrarElArchivo()
    {
        var producto = Producto();
        producto.EstablecerImagen("/uploads/teclado.png");
        PrepararProducto(producto);

        var secuencia = new MockSequence();
        _unitOfWork.InSequence(secuencia)
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _almacenamiento.InSequence(secuencia)
            .Setup(a => a.EliminarAsync("/uploads/teclado.png", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CrearServicio().EliminarAsync(producto.Id);

        producto.ImagenUrl.Should().BeNull();
        _productos.Verify(p => p.Update(producto), Times.Once);
        _almacenamiento.Verify(a => a.EliminarAsync("/uploads/teclado.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EliminarAsync_SinImagen_NoHaceNada()
    {
        var producto = Producto();
        PrepararProducto(producto);

        await CrearServicio().EliminarAsync(producto.Id);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _almacenamiento.Verify(a => a.EliminarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EliminarAsync_CuandoElProductoNoExiste_LanzaExcepcionNoEncontrado()
    {
        var id = Guid.NewGuid();
        _productos.Setup(p => p.GetByIdAsync(id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var act = () => CrearServicio().EliminarAsync(id);

        await act.Should().ThrowAsync<ExcepcionNoEncontrado>();
    }
}
