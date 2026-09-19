using Indigo.Domain.Enums;
using Indigo.Domain.Exceptions;

namespace Indigo.Domain.Entities;

public class Product
{
    private const int LongitudMaximaNombre = 150;

    private Product()
    {
    }

    public Product(string nombre, decimal precio, int stock, CategoriaProducto categoria)
    {
        Id = Guid.NewGuid();
        Nombre = ValidarNombre(nombre);
        Precio = ValidarPrecio(precio);
        Stock = ValidarStock(stock);
        Categoria = ValidarCategoria(categoria);
        Activo = true;
    }

    public Guid Id { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public decimal Precio { get; private set; }

    public int Stock { get; private set; }

    public CategoriaProducto Categoria { get; private set; }

    public string? ImagenUrl { get; private set; }

    public bool Activo { get; private set; }

    public void Actualizar(string nombre, decimal precio, int stock, CategoriaProducto categoria)
    {
        var nombreValidado = ValidarNombre(nombre);
        var precioValidado = ValidarPrecio(precio);
        var stockValidado = ValidarStock(stock);
        var categoriaValidada = ValidarCategoria(categoria);

        Nombre = nombreValidado;
        Precio = precioValidado;
        Stock = stockValidado;
        Categoria = categoriaValidada;
    }

    public void EstablecerImagen(string? imagenUrl)
    {
        ImagenUrl = string.IsNullOrWhiteSpace(imagenUrl) ? null : imagenUrl.Trim();
    }

    public void Desactivar()
    {
        Activo = false;
    }

    public void DescontarStock(int cantidad)
    {
        if (cantidad <= 0)
        {
            throw new ExcepcionReglaDeNegocio("La cantidad a descontar debe ser mayor que cero.");
        }

        if (cantidad > Stock)
        {
            throw new ExcepcionStockInsuficiente(Id, Nombre, cantidad, Stock);
        }

        Stock -= cantidad;
    }

    private static string ValidarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ExcepcionReglaDeNegocio("El nombre del producto es obligatorio.");
        }

        var normalizado = nombre.Trim();

        if (normalizado.Length > LongitudMaximaNombre)
        {
            throw new ExcepcionReglaDeNegocio($"El nombre del producto no puede superar los {LongitudMaximaNombre} caracteres.");
        }

        return normalizado;
    }

    private static decimal ValidarPrecio(decimal precio)
    {
        if (precio <= 0)
        {
            throw new ExcepcionReglaDeNegocio("El precio del producto debe ser mayor que cero.");
        }

        return precio;
    }

    private static int ValidarStock(int stock)
    {
        if (stock < 0)
        {
            throw new ExcepcionReglaDeNegocio("El stock del producto no puede ser negativo.");
        }

        return stock;
    }

    private static CategoriaProducto ValidarCategoria(CategoriaProducto categoria)
    {
        if (!Enum.IsDefined(categoria))
        {
            throw new ExcepcionReglaDeNegocio($"La categoría '{categoria}' no es una categoría válida.");
        }

        return categoria;
    }
}
