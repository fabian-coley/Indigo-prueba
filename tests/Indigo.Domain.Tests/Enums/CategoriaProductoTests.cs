using FluentAssertions;
using Indigo.Domain.Enums;

namespace Indigo.Domain.Tests.Enums;

public class CategoriaProductoTests
{
    [Fact]
    public void LosNombresCoincidenConElContratoHttp()
    {
        Enum.GetNames<CategoriaProducto>().Should().Equal(
            "Electrónica", "Hogar", "Alimentos", "Ropa", "Otros");
    }

    [Fact]
    public void TieneExactamenteCincoCategorias()
    {
        Enum.GetValues<CategoriaProducto>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData("Electrónica")]
    [InlineData("Hogar")]
    [InlineData("Alimentos")]
    [InlineData("Ropa")]
    [InlineData("Otros")]
    public void CadaValorDelContratoEsUnaCategoriaValida(string valor)
    {
        Enum.TryParse<CategoriaProducto>(valor, out var categoria).Should().BeTrue();
        Enum.IsDefined(categoria).Should().BeTrue();
    }

    [Theory]
    [InlineData("electronica")]
    [InlineData("ELECTRÓNICA")]
    [InlineData("Tecnología")]
    [InlineData("")]
    public void UnValorFueraDelContratoNoEsUnaCategoriaValida(string valor)
    {
        Enum.TryParse<CategoriaProducto>(valor, out _).Should().BeFalse();
    }
}
