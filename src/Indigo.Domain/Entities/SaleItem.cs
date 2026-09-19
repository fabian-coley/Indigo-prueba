namespace Indigo.Domain.Entities;

public class SaleItem
{
    private SaleItem()
    {
    }

    internal SaleItem(Guid saleId, Product producto, int cantidad)
    {
        Id = Guid.NewGuid();
        SaleId = saleId;
        ProductId = producto.Id;
        ProductoNombre = producto.Nombre;
        PrecioUnitario = producto.Precio;
        Cantidad = cantidad;
        Subtotal = CalcularSubtotal(PrecioUnitario, cantidad);
    }

    public Guid Id { get; private set; }

    public Guid SaleId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductoNombre { get; private set; } = string.Empty;

    public decimal PrecioUnitario { get; private set; }

    public int Cantidad { get; private set; }

    public decimal Subtotal { get; private set; }

    internal static decimal CalcularSubtotal(decimal precioUnitario, int cantidad) => precioUnitario * cantidad;
}
