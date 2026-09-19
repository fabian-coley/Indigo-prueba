using Indigo.Domain.Enums;

namespace Indigo.Application.Contracts.Productos;

public sealed record ProductoDto(
    Guid Id,
    string Nombre,
    decimal Precio,
    int Stock,
    CategoriaProducto Categoria,
    string? ImagenUrl);
