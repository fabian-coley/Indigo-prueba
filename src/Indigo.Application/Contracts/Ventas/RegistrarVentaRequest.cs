namespace Indigo.Application.Contracts.Ventas;

public sealed record RegistrarVentaRequest(IReadOnlyList<RegistrarVentaItemRequest> Items);
