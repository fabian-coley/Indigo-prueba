using FluentAssertions;
using Indigo.Application.Abstractions.Persistence;
using Indigo.Application.Abstractions.Security;
using Indigo.Application.Common;
using Indigo.Application.Contracts.Ventas;
using Indigo.Application.Services;
using Indigo.Domain.Constants;
using Indigo.Domain.Entities;
using Indigo.Domain.Enums;
using Indigo.Domain.Exceptions;
using Moq;

namespace Indigo.Application.Tests.Services;

public class SaleServiceTests
{
    private const string VendedorId = "vendedor-1";
    private const string AdminId = "admin-1";
    private const string EmailVendedor = "vendedor@indigo.com";

    private readonly Mock<ISaleRepository> _ventas = new();
    private readonly Mock<IProductRepository> _productos = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IIdentityService> _identity = new();

    private SaleService CrearServicio(ICurrentUserService usuarioActual) =>
        new(_ventas.Object, _productos.Object, _unitOfWork.Object, usuarioActual, _identity.Object);

    private static ICurrentUserService Usuario(string? usuarioId, bool esAdmin)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(u => u.UsuarioId).Returns(usuarioId);
        mock.SetupGet(u => u.EstaAutenticado).Returns(usuarioId is not null);
        mock.SetupGet(u => u.EsAdmin).Returns(esAdmin);
        mock.SetupGet(u => u.Roles).Returns(esAdmin ? [Roles.Admin] : [Roles.Vendedor]);
        return mock.Object;
    }

    private static Product Producto(string nombre = "Teclado mecánico", decimal precio = 100m, int stock = 10) =>
        new(nombre, precio, stock, CategoriaProducto.Electrónica);

    private void PrepararProducto(Product producto) =>
        _productos.Setup(p => p.GetByIdAsync(producto.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(producto);

    private void ConfigurarTransaccion(MockSequence? secuencia = null)
    {
        if (secuencia is null)
        {
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }
        else
        {
            _unitOfWork.InSequence(secuencia).Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.InSequence(secuencia).Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _unitOfWork.InSequence(secuencia).Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }

        _unitOfWork.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task RegistrarAsync_ConStockSuficiente_ConfirmaLaTransaccionEnOrden()
    {
        var teclado = Producto(stock: 10);
        var mouse = Producto("Mouse", precio: 25.5m, stock: 4);
        PrepararProducto(teclado);
        PrepararProducto(mouse);

        var secuencia = new MockSequence();
        ConfigurarTransaccion(secuencia);

        _identity.Setup(i => i.ObtenerEmailAsync(VendedorId, It.IsAny<CancellationToken>())).ReturnsAsync(EmailVendedor);

        var request = new RegistrarVentaRequest(
        [
            new RegistrarVentaItemRequest(teclado.Id, 2),
            new RegistrarVentaItemRequest(mouse.Id, 4)
        ]);

        var detalle = await CrearServicio(Usuario(VendedorId, esAdmin: false)).RegistrarAsync(request);

        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);

        _ventas.Verify(v => v.Add(It.Is<Sale>(venta =>
            venta.UsuarioId == VendedorId &&
            venta.Total == 302m &&
            venta.CantidadItems == 6 &&
            venta.Items.Count == 2)), Times.Once);

        teclado.Stock.Should().Be(8);
        mouse.Stock.Should().Be(0);

        detalle.Total.Should().Be(302m);
        detalle.CantidadItems.Should().Be(6);
        detalle.UsuarioEmail.Should().Be(EmailVendedor);
        detalle.Items.Should().HaveCount(2);
        detalle.Items[0].ProductoNombre.Should().Be("Teclado mecánico");
        detalle.Items[0].Subtotal.Should().Be(200m);
    }

    [Fact]
    public async Task RegistrarAsync_BuscaLosProductosIncluyendoLosInactivos()
    {
        var producto = Producto();
        PrepararProducto(producto);
        ConfigurarTransaccion();
        _identity.Setup(i => i.ObtenerEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(EmailVendedor);

        var request = new RegistrarVentaRequest([new RegistrarVentaItemRequest(producto.Id, 1)]);

        await CrearServicio(Usuario(VendedorId, esAdmin: false)).RegistrarAsync(request);

        _productos.Verify(p => p.GetByIdAsync(producto.Id, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarAsync_ConStockInsuficiente_RevierteYNoPersiste()
    {
        var producto = Producto(stock: 3);
        PrepararProducto(producto);
        ConfigurarTransaccion();

        var request = new RegistrarVentaRequest([new RegistrarVentaItemRequest(producto.Id, 4)]);

        var act = () => CrearServicio(Usuario(VendedorId, esAdmin: false)).RegistrarAsync(request);

        await act.Should().ThrowAsync<ExcepcionStockInsuficiente>();

        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _ventas.Verify(v => v.Add(It.IsAny<Sale>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarAsync_CuandoUnProductoNoExiste_RevierteTodaLaVenta()
    {
        var existente = Producto();
        PrepararProducto(existente);
        ConfigurarTransaccion();

        var request = new RegistrarVentaRequest(
        [
            new RegistrarVentaItemRequest(existente.Id, 1),
            new RegistrarVentaItemRequest(Guid.NewGuid(), 1)
        ]);

        var act = () => CrearServicio(Usuario(VendedorId, esAdmin: false)).RegistrarAsync(request);

        await act.Should().ThrowAsync<ExcepcionReglaDeNegocio>();
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _ventas.Verify(v => v.Add(It.IsAny<Sale>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarAsync_ConProductoDadoDeBaja_Revierte()
    {
        var producto = Producto();
        producto.Desactivar();
        PrepararProducto(producto);
        ConfigurarTransaccion();

        var request = new RegistrarVentaRequest([new RegistrarVentaItemRequest(producto.Id, 1)]);

        var act = () => CrearServicio(Usuario(VendedorId, esAdmin: false)).RegistrarAsync(request);

        await act.Should().ThrowAsync<ExcepcionReglaDeNegocio>();
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarAsync_SinItems_Revierte()
    {
        ConfigurarTransaccion();

        var request = new RegistrarVentaRequest([]);

        var act = () => CrearServicio(Usuario(VendedorId, esAdmin: false)).RegistrarAsync(request);

        await act.Should().ThrowAsync<ExcepcionReglaDeNegocio>();
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _ventas.Verify(v => v.Add(It.IsAny<Sale>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RegistrarAsync_ConCantidadNoPositiva_Revierte(int cantidad)
    {
        var producto = Producto();
        PrepararProducto(producto);
        ConfigurarTransaccion();

        var request = new RegistrarVentaRequest([new RegistrarVentaItemRequest(producto.Id, cantidad)]);

        var act = () => CrearServicio(Usuario(VendedorId, esAdmin: false)).RegistrarAsync(request);

        await act.Should().ThrowAsync<ExcepcionReglaDeNegocio>();
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _ventas.Verify(v => v.Add(It.IsAny<Sale>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarAsync_ConElMismoProductoEnDosLineas_AcumulaElDescuento()
    {
        var producto = Producto(precio: 10m, stock: 30);
        PrepararProducto(producto);
        ConfigurarTransaccion();
        _identity.Setup(i => i.ObtenerEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(EmailVendedor);

        var request = new RegistrarVentaRequest(
        [
            new RegistrarVentaItemRequest(producto.Id, 20),
            new RegistrarVentaItemRequest(producto.Id, 5)
        ]);

        var detalle = await CrearServicio(Usuario(VendedorId, esAdmin: false)).RegistrarAsync(request);

        producto.Stock.Should().Be(5);
        detalle.CantidadItems.Should().Be(25);
        detalle.Total.Should().Be(250m);
    }

    [Fact]
    public async Task RegistrarAsync_SinUsuarioAutenticado_FallaSinAbrirTransaccion()
    {
        var request = new RegistrarVentaRequest([new RegistrarVentaItemRequest(Guid.NewGuid(), 1)]);

        var act = () => CrearServicio(Usuario(null, esAdmin: false)).RegistrarAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_ComoAdmin_DevuelveCualquierVenta()
    {
        var venta = new Sale(VendedorId, DateTimeOffset.UtcNow);
        venta.AgregarItem(Producto(), 1);

        _ventas.Setup(v => v.GetByIdAsync(venta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(venta);
        _identity.Setup(i => i.ObtenerEmailAsync(VendedorId, It.IsAny<CancellationToken>())).ReturnsAsync(EmailVendedor);

        var detalle = await CrearServicio(Usuario(AdminId, esAdmin: true)).ObtenerPorIdAsync(venta.Id);

        detalle.Should().NotBeNull();
        detalle!.UsuarioEmail.Should().Be(EmailVendedor);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_ComoVendedorPropietario_DevuelveLaVenta()
    {
        var venta = new Sale(VendedorId, DateTimeOffset.UtcNow);
        venta.AgregarItem(Producto(), 2);

        _ventas.Setup(v => v.GetByIdAsync(venta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(venta);
        _identity.Setup(i => i.ObtenerEmailAsync(VendedorId, It.IsAny<CancellationToken>())).ReturnsAsync(EmailVendedor);

        var detalle = await CrearServicio(Usuario(VendedorId, esAdmin: false)).ObtenerPorIdAsync(venta.Id);

        detalle.Should().NotBeNull();
        detalle!.CantidadItems.Should().Be(2);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_ComoVendedorDeOtro_DevuelveNull()
    {
        var venta = new Sale("otro-vendedor", DateTimeOffset.UtcNow);
        venta.AgregarItem(Producto(), 1);

        _ventas.Setup(v => v.GetByIdAsync(venta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(venta);

        var detalle = await CrearServicio(Usuario(VendedorId, esAdmin: false)).ObtenerPorIdAsync(venta.Id);

        detalle.Should().BeNull();
        _identity.Verify(i => i.ObtenerEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_CuandoNoExiste_DevuelveNull()
    {
        var id = Guid.NewGuid();
        _ventas.Setup(v => v.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Sale?)null);

        var detalle = await CrearServicio(Usuario(VendedorId, esAdmin: false)).ObtenerPorIdAsync(id);

        detalle.Should().BeNull();
    }

    [Fact]
    public async Task ListarAsync_ComoAdmin_NoFiltraPorUsuario()
    {
        _ventas.Setup(v => v.GetPagedAsync(It.IsAny<VentaFiltro>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<VentaResumen>([], 1, 10, 0));

        await CrearServicio(Usuario(AdminId, esAdmin: true)).ListarAsync(1, 10);

        _ventas.Verify(v => v.GetPagedAsync(
            It.Is<VentaFiltro>(f => f.UsuarioId == null && f.Page == 1 && f.PageSize == 10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListarAsync_ComoVendedor_FiltraPorSuPropioId()
    {
        _ventas.Setup(v => v.GetPagedAsync(It.IsAny<VentaFiltro>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<VentaResumen>([], 1, 10, 0));

        await CrearServicio(Usuario(VendedorId, esAdmin: false)).ListarAsync(1, 10);

        _ventas.Verify(v => v.GetPagedAsync(
            It.Is<VentaFiltro>(f => f.UsuarioId == VendedorId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListarAsync_MapeaElResumenAlDto()
    {
        var resumen = new VentaResumen(Guid.NewGuid(), DateTimeOffset.UtcNow, 302m, 6, EmailVendedor);
        _ventas.Setup(v => v.GetPagedAsync(It.IsAny<VentaFiltro>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<VentaResumen>([resumen], 1, 10, 1));

        var pagina = await CrearServicio(Usuario(VendedorId, esAdmin: false)).ListarAsync(1, 10);

        pagina.Items.Should().ContainSingle();
        pagina.Items[0].Total.Should().Be(302m);
        pagina.Items[0].CantidadItems.Should().Be(6);
        pagina.Items[0].UsuarioEmail.Should().Be(EmailVendedor);
        pagina.TotalPages.Should().Be(1);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ListarAsync_ConPaginacionInvalida_LanzaSinConsultarElRepositorio(int page, int pageSize)
    {
        var act = () => CrearServicio(Usuario(VendedorId, esAdmin: false)).ListarAsync(page, pageSize);

        await act.Should().ThrowAsync<ExcepcionReglaDeNegocio>();
        _ventas.Verify(v => v.GetPagedAsync(It.IsAny<VentaFiltro>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
