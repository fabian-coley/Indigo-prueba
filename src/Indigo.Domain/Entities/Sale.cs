using Indigo.Domain.Exceptions;

namespace Indigo.Domain.Entities;

public class Sale
{
    private readonly List<SaleItem> _items = [];

    private Sale()
    {
    }

    public Sale(string usuarioId, DateTimeOffset fecha)
    {
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            throw new ExcepcionReglaDeNegocio("La venta debe estar asociada a un usuario.");
        }

        Id = Guid.NewGuid();
        UsuarioId = usuarioId.Trim();
        Fecha = fecha;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset Fecha { get; private set; }

    public string UsuarioId { get; private set; } = string.Empty;

    public decimal Total { get; private set; }

    public int CantidadItems => _items.Sum(item => item.Cantidad);

    public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();

    public SaleItem AgregarItem(Product producto, int cantidad)
    {
        ArgumentNullException.ThrowIfNull(producto);

        if (cantidad <= 0)
        {
            throw new ExcepcionReglaDeNegocio("La cantidad de cada ítem de la venta debe ser mayor que cero.");
        }

        if (!producto.Activo)
        {
            throw new ExcepcionReglaDeNegocio($"El producto '{producto.Nombre}' no está disponible para la venta.");
        }

        producto.DescontarStock(cantidad);

        var item = new SaleItem(Id, producto, cantidad);
        _items.Add(item);
        RecalcularTotal();

        return item;
    }

    public void ValidarParaRegistrar()
    {
        if (_items.Count == 0)
        {
            throw new ExcepcionReglaDeNegocio("La venta debe tener al menos un ítem.");
        }
    }

    private void RecalcularTotal()
    {
        Total = _items.Sum(item => item.Subtotal);
    }
}
