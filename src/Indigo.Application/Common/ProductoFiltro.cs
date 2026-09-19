using Indigo.Domain.Enums;

namespace Indigo.Application.Common;

public sealed record ProductoFiltro(
    string? Search,
    CategoriaProducto? Categoria,
    int Page,
    int PageSize);
