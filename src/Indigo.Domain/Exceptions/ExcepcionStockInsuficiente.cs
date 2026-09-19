namespace Indigo.Domain.Exceptions;

public sealed class ExcepcionStockInsuficiente : DomainException
{
    public ExcepcionStockInsuficiente(Guid productoId, string productoNombre, int solicitado, int disponible)
        : base($"Stock insuficiente para '{productoNombre}': se solicitaron {solicitado} unidad(es) y hay {disponible} disponible(s).")
    {
        ProductoId = productoId;
        ProductoNombre = productoNombre;
        Solicitado = solicitado;
        Disponible = disponible;
    }

    public Guid ProductoId { get; }

    public string ProductoNombre { get; }

    public int Solicitado { get; }

    public int Disponible { get; }
}
