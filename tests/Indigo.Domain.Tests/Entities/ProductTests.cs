using FluentAssertions;
using Indigo.Domain.Entities;
using Indigo.Domain.Enums;
using Indigo.Domain.Exceptions;

namespace Indigo.Domain.Tests.Entities;

public class ProductTests
{
    private const decimal PrecioValido = 120.50m;
    private const int StockValido = 10;

    private static Product CrearProducto(
        string nombre = "Teclado mecánico",
        decimal precio = PrecioValido,
        int stock = StockValido,
        CategoriaProducto categoria = CategoriaProducto.Electrónica)
        => new(nombre, precio, stock, categoria);

    [Fact]
    public void Constructor_ConDatosValidos_CreaElProductoActivo()
    {
        var producto = CrearProducto();

        producto.Id.Should().NotBe(Guid.Empty);
        producto.Nombre.Should().Be("Teclado mecánico");
        producto.Precio.Should().Be(PrecioValido);
        producto.Stock.Should().Be(StockValido);
        producto.Categoria.Should().Be(CategoriaProducto.Electrónica);
        producto.ImagenUrl.Should().BeNull();
        producto.Activo.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ConStockCero_EsValido()
    {
        var producto = CrearProducto(stock: 0);

        producto.Stock.Should().Be(0);
    }

    [Fact]
    public void Constructor_NormalizaElNombre()
    {
        var producto = CrearProducto(nombre: "  Mouse inalámbrico  ");

        producto.Nombre.Should().Be("Mouse inalámbrico");
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(-0.01d)]
    public void Constructor_ConPrecioNoPositivo_LanzaExcepcionReglaDeNegocio(double precio)
    {
        var accion = () => CrearProducto(precio: (decimal)precio);

        accion.Should().Throw<ExcepcionReglaDeNegocio>()
            .WithMessage("*precio*mayor que cero*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ConStockNegativo_LanzaExcepcionReglaDeNegocio(int stock)
    {
        var accion = () => CrearProducto(stock: stock);

        accion.Should().Throw<ExcepcionReglaDeNegocio>()
            .WithMessage("*stock*no puede ser negativo*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_SinNombre_LanzaExcepcionReglaDeNegocio(string? nombre)
    {
        var accion = () => CrearProducto(nombre: nombre!);

        accion.Should().Throw<ExcepcionReglaDeNegocio>()
            .WithMessage("*nombre*obligatorio*");
    }

    [Fact]
    public void Constructor_ConNombreDemasiadoLargo_LanzaExcepcionReglaDeNegocio()
    {
        var accion = () => CrearProducto(nombre: new string('x', 151));

        accion.Should().Throw<ExcepcionReglaDeNegocio>()
            .WithMessage("*no puede superar*150*");
    }

    [Fact]
    public void Constructor_ConCategoriaFueraDelEnum_LanzaExcepcionReglaDeNegocio()
    {
        var accion = () => CrearProducto(categoria: (CategoriaProducto)99);

        accion.Should().Throw<ExcepcionReglaDeNegocio>()
            .WithMessage("*no es una categoría válida*");
    }

    [Fact]
    public void Actualizar_ConDatosValidos_ReemplazaLosCampos()
    {
        var producto = CrearProducto();

        producto.Actualizar("Silla ergonómica", 350m, 4, CategoriaProducto.Hogar);

        producto.Nombre.Should().Be("Silla ergonómica");
        producto.Precio.Should().Be(350m);
        producto.Stock.Should().Be(4);
        producto.Categoria.Should().Be(CategoriaProducto.Hogar);
    }

    [Fact]
    public void Actualizar_NoTocaLaImagenNiElEstado()
    {
        var producto = CrearProducto();
        producto.EstablecerImagen("/uploads/abc.png");

        producto.Actualizar("Teclado mecánico RGB", 200m, 8, CategoriaProducto.Electrónica);

        producto.ImagenUrl.Should().Be("/uploads/abc.png");
        producto.Activo.Should().BeTrue();
    }

    [Fact]
    public void Actualizar_ConDatosInvalidos_NoModificaElProducto()
    {
        var producto = CrearProducto();

        var accion = () => producto.Actualizar("Teclado", 0m, 8, CategoriaProducto.Electrónica);

        accion.Should().Throw<ExcepcionReglaDeNegocio>();
        producto.Precio.Should().Be(PrecioValido);
        producto.Nombre.Should().Be("Teclado mecánico");
    }

    [Fact]
    public void DescontarStock_ConCantidadDisponible_ReduceElStock()
    {
        var producto = CrearProducto(stock: 10);

        producto.DescontarStock(4);

        producto.Stock.Should().Be(6);
    }

    [Fact]
    public void DescontarStock_ConTodoElStockDisponible_LoDejaEnCero()
    {
        var producto = CrearProducto(stock: 3);

        producto.DescontarStock(3);

        producto.Stock.Should().Be(0);
    }

    [Fact]
    public void DescontarStock_ConCantidadMayorAlDisponible_LanzaExcepcionStockInsuficienteConElConflicto()
    {
        var producto = CrearProducto(stock: 3);

        var accion = () => producto.DescontarStock(5);

        accion.Should().Throw<ExcepcionStockInsuficiente>()
            .Which.Should().Match<ExcepcionStockInsuficiente>(excepcion =>
                excepcion.ProductoId == producto.Id
                && excepcion.Solicitado == 5
                && excepcion.Disponible == 3);
    }

    [Fact]
    public void DescontarStock_ConCantidadMayorAlDisponible_NoModificaElStock()
    {
        var producto = CrearProducto(stock: 3);

        var accion = () => producto.DescontarStock(5);

        accion.Should().Throw<ExcepcionStockInsuficiente>();
        producto.Stock.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DescontarStock_ConCantidadNoPositiva_LanzaExcepcionReglaDeNegocio(int cantidad)
    {
        var producto = CrearProducto();

        var accion = () => producto.DescontarStock(cantidad);

        accion.Should().Throw<ExcepcionReglaDeNegocio>()
            .WithMessage("*cantidad a descontar*mayor que cero*");
    }

    [Fact]
    public void Desactivar_MarcaElProductoComoInactivo()
    {
        var producto = CrearProducto();

        producto.Desactivar();

        producto.Activo.Should().BeFalse();
    }

    [Fact]
    public void EstablecerImagen_ConUrlAsociaLaImagen()
    {
        var producto = CrearProducto();

        producto.EstablecerImagen("  /uploads/foto.png  ");

        producto.ImagenUrl.Should().Be("/uploads/foto.png");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EstablecerImagen_SinUrl_LimpiaLaImagen(string? imagenUrl)
    {
        var producto = CrearProducto();
        producto.EstablecerImagen("/uploads/foto.png");

        producto.EstablecerImagen(imagenUrl);

        producto.ImagenUrl.Should().BeNull();
    }
}
