using FluentAssertions;
using Indigo.Domain.Entities;
using Indigo.Domain.Enums;
using Indigo.Domain.Exceptions;

namespace Indigo.Domain.Tests.Entities;

public class SaleTests
{
    private const string UsuarioId = "usuario-de-prueba";
    private const decimal PrecioMonitor = 100m;

    private static Product CrearProducto(
        string nombre = "Monitor LED",
        decimal precio = PrecioMonitor,
        int stock = 10)
        => new(nombre, precio, stock, CategoriaProducto.Electrónica);

    private static Sale CrearVenta() => new(UsuarioId, DateTimeOffset.UtcNow);

    [Fact]
    public void Constructor_GuardaElUsuarioYLaFecha()
    {
        var fecha = new DateTimeOffset(2026, 9, 19, 14, 30, 0, TimeSpan.FromHours(-3));

        var venta = new Sale(UsuarioId, fecha);

        venta.Id.Should().NotBe(Guid.Empty);
        venta.UsuarioId.Should().Be(UsuarioId);
        venta.Fecha.Should().Be(fecha);
        venta.Total.Should().Be(0m);
        venta.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_SinUsuario_LanzaExcepcionReglaDeNegocio(string? usuarioId)
    {
        var accion = () => new Sale(usuarioId!, DateTimeOffset.UtcNow);

        accion.Should().Throw<ExcepcionReglaDeNegocio>()
            .WithMessage("*asociada a un usuario*");
    }

    [Fact]
    public void AgregarItem_GuardaElSubtotalComoPrecioPorCantidad()
    {
        var producto = CrearProducto(precio: 120.50m);
        var venta = CrearVenta();

        var item = venta.AgregarItem(producto, 3);

        item.PrecioUnitario.Should().Be(120.50m);
        item.Cantidad.Should().Be(3);
        item.Subtotal.Should().Be(361.50m);
    }

    [Fact]
    public void AgregarItem_TomaUnSnapshotDelNombreYPrecioDelProducto()
    {
        var producto = CrearProducto(nombre: "Monitor LED", precio: 100m);
        var venta = CrearVenta();
        var item = venta.AgregarItem(producto, 2);

        producto.Actualizar("Monitor LED 27\"", 999m, 50, CategoriaProducto.Electrónica);

        item.ProductoNombre.Should().Be("Monitor LED");
        item.PrecioUnitario.Should().Be(100m);
        item.Subtotal.Should().Be(200m);
        venta.Total.Should().Be(200m);
    }

    [Fact]
    public void AgregarItem_DescuentaElStockDelProducto()
    {
        var producto = CrearProducto(stock: 10);
        var venta = CrearVenta();

        venta.AgregarItem(producto, 4);

        producto.Stock.Should().Be(6);
    }

    [Fact]
    public void AgregarItem_EnVariasLineasDelMismoProducto_DescuentaElStockAcumulado()
    {
        var producto = CrearProducto(stock: 10);
        var venta = CrearVenta();

        venta.AgregarItem(producto, 4);
        venta.AgregarItem(producto, 6);

        producto.Stock.Should().Be(0);
        venta.Items.Should().HaveCount(2);
        venta.CantidadItems.Should().Be(10);
    }

    [Fact]
    public void AgregarItem_ConCantidadMayorAlStock_LanzaExcepcionStockInsuficiente()
    {
        var producto = CrearProducto(stock: 3);
        var venta = CrearVenta();

        var accion = () => venta.AgregarItem(producto, 5);

        accion.Should().Throw<ExcepcionStockInsuficiente>()
            .WithMessage("*Stock insuficiente*");
    }

    [Fact]
    public void AgregarItem_ConCantidadMayorAlStock_NoAgregaElItemNiDescuentaStock()
    {
        var producto = CrearProducto(stock: 3);
        var venta = CrearVenta();

        var accion = () => venta.AgregarItem(producto, 5);

        accion.Should().Throw<ExcepcionStockInsuficiente>();
        venta.Items.Should().BeEmpty();
        venta.Total.Should().Be(0m);
        producto.Stock.Should().Be(3);
    }

    [Fact]
    public void AgregarItem_ConStockAgotadoPorUnaLineaAnterior_LanzaExcepcionStockInsuficiente()
    {
        var producto = CrearProducto(stock: 10);
        var venta = CrearVenta();
        venta.AgregarItem(producto, 8);

        var accion = () => venta.AgregarItem(producto, 3);

        accion.Should().Throw<ExcepcionStockInsuficiente>();
        venta.Items.Should().HaveCount(1);
        producto.Stock.Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AgregarItem_ConCantidadNoPositiva_LanzaExcepcionReglaDeNegocio(int cantidad)
    {
        var producto = CrearProducto();
        var venta = CrearVenta();

        var accion = () => venta.AgregarItem(producto, cantidad);

        accion.Should().Throw<ExcepcionReglaDeNegocio>()
            .WithMessage("*cantidad*mayor que cero*");
        venta.Items.Should().BeEmpty();
    }

    [Fact]
    public void AgregarItem_ConProductoInactivo_LanzaExcepcionReglaDeNegocio()
    {
        var producto = CrearProducto();
        producto.Desactivar();
        var venta = CrearVenta();

        var accion = () => venta.AgregarItem(producto, 1);

        accion.Should().Throw<ExcepcionReglaDeNegocio>()
            .WithMessage("*no está disponible para la venta*");
        venta.Items.Should().BeEmpty();
        producto.Stock.Should().Be(10);
    }

    [Fact]
    public void AgregarItem_SinProducto_LanzaArgumentNullException()
    {
        var venta = CrearVenta();

        var accion = () => venta.AgregarItem(null!, 1);

        accion.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Total_EsLaSumaDeLosSubtotalesDeTodasLasLineas()
    {
        var monitor = CrearProducto(nombre: "Monitor LED", precio: 100m, stock: 10);
        var teclado = CrearProducto(nombre: "Teclado", precio: 25.50m, stock: 10);
        var venta = CrearVenta();

        venta.AgregarItem(monitor, 2);
        venta.Total.Should().Be(200m);

        venta.AgregarItem(teclado, 4);
        venta.Total.Should().Be(302m);

        venta.Items.Sum(item => item.Subtotal).Should().Be(venta.Total);
    }

    [Fact]
    public void CantidadItems_SumaUnidadesYNoLineas()
    {
        var monitor = CrearProducto(nombre: "Monitor LED", stock: 10);
        var teclado = CrearProducto(nombre: "Teclado", precio: 25m, stock: 10);
        var venta = CrearVenta();

        venta.AgregarItem(monitor, 3);
        venta.AgregarItem(teclado, 2);

        venta.Items.Should().HaveCount(2);
        venta.CantidadItems.Should().Be(5);
    }

    [Fact]
    public void ValidarParaRegistrar_ConVentaVacia_LanzaExcepcionReglaDeNegocio()
    {
        var venta = CrearVenta();

        var accion = () => venta.ValidarParaRegistrar();

        accion.Should().Throw<ExcepcionReglaDeNegocio>()
            .WithMessage("*al menos un ítem*");
    }

    [Fact]
    public void ValidarParaRegistrar_ConAlMenosUnItem_NoLanza()
    {
        var venta = CrearVenta();
        venta.AgregarItem(CrearProducto(), 1);

        var accion = () => venta.ValidarParaRegistrar();

        accion.Should().NotThrow();
    }

    [Fact]
    public void Items_ExponeUnaVistaDeSoloLecturaDeLosItems()
    {
        var venta = CrearVenta();
        venta.AgregarItem(CrearProducto(), 1);

        venta.Items.Should().ContainSingle()
            .Which.SaleId.Should().Be(venta.Id);
    }
}
