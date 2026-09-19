namespace Indigo.Application.Contracts.Ventas;

public sealed record RegistrarVentaItemRequest(Guid ProductoId, int Cantidad);
