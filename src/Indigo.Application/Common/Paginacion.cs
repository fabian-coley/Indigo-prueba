using Indigo.Domain.Exceptions;

namespace Indigo.Application.Common;

public static class Paginacion
{
    public const int PageSizePredeterminado = 10;
    public const int PageSizeMaximo = 100;

    public static void Validar(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new ExcepcionReglaDeNegocio("El parámetro 'page' debe ser mayor o igual a 1.");
        }

        if (pageSize < 1 || pageSize > PageSizeMaximo)
        {
            throw new ExcepcionReglaDeNegocio(
                $"El parámetro 'pageSize' debe estar entre 1 y {PageSizeMaximo}.");
        }
    }
}
