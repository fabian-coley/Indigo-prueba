using FluentAssertions;
using Indigo.Application.Common;
using Indigo.Domain.Exceptions;

namespace Indigo.Application.Tests.Common;

public class PaginacionTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, Paginacion.PageSizePredeterminado)]
    [InlineData(50, Paginacion.PageSizeMaximo)]
    public void Validar_ConValoresEnRango_NoLanza(int page, int pageSize)
    {
        var act = () => Paginacion.Validar(page, pageSize);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validar_ConPageMenorAUno_Lanza(int page)
    {
        var act = () => Paginacion.Validar(page, Paginacion.PageSizePredeterminado);

        act.Should().Throw<ExcepcionReglaDeNegocio>().WithMessage("*page*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Paginacion.PageSizeMaximo + 1)]
    public void Validar_ConPageSizeFueraDeRango_Lanza(int pageSize)
    {
        var act = () => Paginacion.Validar(1, pageSize);

        act.Should().Throw<ExcepcionReglaDeNegocio>().WithMessage("*pageSize*");
    }

    [Theory]
    [InlineData(12, 10, 2)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(0, 10, 0)]
    [InlineData(1, 100, 1)]
    public void TotalPages_RedondeaHaciaArriba(int totalItems, int pageSize, int esperado)
    {
        var pagina = new PagedResult<int>([], 1, pageSize, totalItems);

        pagina.TotalPages.Should().Be(esperado);
    }
}
