using Indigo.Domain.Enums;

namespace Indigo.Application.Contracts.Productos;

public sealed record ActualizarProductoRequest(string Nombre, decimal Precio, int Stock, CategoriaProducto Categoria);
