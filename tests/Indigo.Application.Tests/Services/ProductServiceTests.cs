using FluentAssertions;
using Indigo.Application.Abstractions.Persistence;
using Indigo.Application.Abstractions.Storage;
using Indigo.Application.Common;
using Indigo.Application.Contracts.Productos;
using Indigo.Application.Services;
using Indigo.Domain.Entities;
using Indigo.Domain.Enums;
using Indigo.Domain.Exceptions;
using Moq;

namespace Indigo.Application.Tests.Services;

public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _productos = new();
    private readonly Mock<IBlobStorage> _almacenamiento = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private ProductService CrearServicio() => new(_productos.Object, _almacenamiento.Object, _unitOfWork.Object);

    private static Product Producto(
        string nombre = "Teclado mecánico",
        decimal precio = 49.99m,
        int stock = 10,
        CategoriaProducto categoria = CategoriaProducto.Electrónica) => new(nombre, precio, stock, categoria);

    private static ProductoFiltro Filtro(int page = 1, int pageSize = 10) =>
        new(null, null, page, pageSize);

    [Fact]
    public async Task ListarAsync_MapeaLaPaginaConservandoLaPaginacion()
    {
        var pagina = new PagedResult<Product>([Producto(), Producto("Mouse")], Page: 2, PageSize: 10, TotalItems: 12);

        _productos.Setup(p => p.GetPagedAsync(It.IsAny<ProductoFiltro>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagina);

        var resultado = await CrearServicio().ListarAsync(Filtro(page: 2));

        resultado.Items.Should().HaveCount(2);
        resultado.Page.Should().Be(2);
        resultado.PageSize.Should().Be(10);
        resultado.TotalItems.Should().Be(12);
        resultado.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task ListarAsync_PasaElFiltroTalCualAlRepositorio()
    {
        var filtro = new ProductoFiltro("teclado", CategoriaProducto.Electrónica, 3, 25);

        _productos.Setup(p => p.GetPagedAsync(It.IsAny<ProductoFiltro>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Product>([], 3, 25, 0));

        await CrearServicio().ListarAsync(filtro);

        _productos.Verify(p => p.GetPagedAsync(filtro, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    [InlineData(1, Paginacion.PageSizeMaximo + 1)]
    public async Task ListarAsync_ConPaginacionInvalida_LanzaSinConsultarElRepositorio(int page, int pageSize)
    {
        var act = () => CrearServicio().ListarAsync(Filtro(page, pageSize));

        await act.Should().ThrowAsync<ExcepcionReglaDeNegocio>();

        _productos.Verify(p => p.GetPagedAsync(It.IsAny<ProductoFiltro>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_CuandoExiste_DevuelveElDto()
    {
        var producto = Producto();

        _productos.Setup(p => p.GetByIdAsync(producto.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(producto);

        var resultado = await CrearServicio().ObtenerPorIdAsync(producto.Id);

        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(producto.Id);
        resultado.Nombre.Should().Be(producto.Nombre);
        resultado.Precio.Should().Be(producto.Precio);
        resultado.Stock.Should().Be(producto.Stock);
        resultado.Categoria.Should().Be(producto.Categoria);
        resultado.ImagenUrl.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerPorIdAsync_CuandoNoExiste_DevuelveNull()
    {
        var id = Guid.NewGuid();

        _productos.Setup(p => p.GetByIdAsync(id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var resultado = await CrearServicio().ObtenerPorIdAsync(id);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task CrearAsync_PersisteYDevuelveElProducto()
    {
        var request = new CrearProductoRequest("Monitor 27\"", 249.90m, 7, CategoriaProducto.Electrónica);

        var resultado = await CrearServicio().CrearAsync(request);

        _productos.Verify(p => p.Add(It.Is<Product>(producto =>
            producto.Nombre == request.Nombre &&
            producto.Precio == request.Precio &&
            producto.Stock == request.Stock &&
            producto.Categoria == request.Categoria)), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        resultado.Id.Should().NotBe(Guid.Empty);
        resultado.Nombre.Should().Be(request.Nombre);
        resultado.ImagenUrl.Should().BeNull();
    }

    [Fact]
    public async Task CrearAsync_ConPrecioInvalido_LanzaYNoPersiste()
    {
        var request = new CrearProductoRequest("Regalo", 0m, 7, CategoriaProducto.Otros);

        var act = () => CrearServicio().CrearAsync(request);

        await act.Should().ThrowAsync<ExcepcionReglaDeNegocio>();
        _productos.Verify(p => p.Add(It.IsAny<Product>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActualizarAsync_CuandoExiste_ActualizaYPersiste()
    {
        var producto = Producto();
        var request = new ActualizarProductoRequest("Teclado inalámbrico", 59.99m, 4, CategoriaProducto.Hogar);

        _productos.Setup(p => p.GetByIdAsync(producto.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(producto);

        var resultado = await CrearServicio().ActualizarAsync(producto.Id, request);

        _productos.Verify(p => p.Update(producto), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        resultado.Nombre.Should().Be(request.Nombre);
        resultado.Precio.Should().Be(request.Precio);
        resultado.Stock.Should().Be(request.Stock);
        resultado.Categoria.Should().Be(request.Categoria);
    }

    [Fact]
    public async Task ActualizarAsync_CuandoNoExiste_LanzaExcepcionNoEncontrado()
    {
        var id = Guid.NewGuid();

        _productos.Setup(p => p.GetByIdAsync(id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var act = () => CrearServicio().ActualizarAsync(id, new ActualizarProductoRequest("X", 1m, 1, CategoriaProducto.Otros));

        await act.Should().ThrowAsync<ExcepcionNoEncontrado>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActualizarAsync_ConDatosInvalidos_NoModificaNiPersiste()
    {
        var producto = Producto();
        var request = new ActualizarProductoRequest("Teclado inválido", -1m, 4, CategoriaProducto.Hogar);

        _productos.Setup(p => p.GetByIdAsync(producto.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(producto);

        var act = () => CrearServicio().ActualizarAsync(producto.Id, request);

        await act.Should().ThrowAsync<ExcepcionReglaDeNegocio>();

        producto.Nombre.Should().Be("Teclado mecánico");
        producto.Precio.Should().Be(49.99m);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EliminarAsync_ConImagen_BorraElArchivoDespuesDePersistirLaBaja()
    {
        var producto = Producto();
        producto.EstablecerImagen("/uploads/teclado.png");

        _productos.Setup(p => p.GetByIdAsync(producto.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(producto);

        await CrearServicio().EliminarAsync(producto.Id);

        producto.Activo.Should().BeFalse();
        _productos.Verify(p => p.Delete(producto), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _almacenamiento.Verify(a => a.EliminarAsync("/uploads/teclado.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EliminarAsync_SinImagen_NoTocaElAlmacenamiento()
    {
        var producto = Producto();

        _productos.Setup(p => p.GetByIdAsync(producto.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(producto);

        await CrearServicio().EliminarAsync(producto.Id);

        producto.Activo.Should().BeFalse();
        _almacenamiento.Verify(a => a.EliminarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EliminarAsync_CuandoNoExiste_LanzaExcepcionNoEncontrado()
    {
        var id = Guid.NewGuid();

        _productos.Setup(p => p.GetByIdAsync(id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var act = () => CrearServicio().EliminarAsync(id);

        await act.Should().ThrowAsync<ExcepcionNoEncontrado>();
        _productos.Verify(p => p.Delete(It.IsAny<Product>()), Times.Never);
    }
}
