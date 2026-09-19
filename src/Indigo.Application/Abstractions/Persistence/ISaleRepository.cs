using Indigo.Application.Common;
using Indigo.Domain.Entities;

namespace Indigo.Application.Abstractions.Persistence;

public interface ISaleRepository
{
    void Add(Sale venta);

    Task<Sale?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<VentaResumen>> GetPagedAsync(VentaFiltro filtro, CancellationToken ct = default);

    Task<IReadOnlyList<VentaResumen>> GetPorRangoAsync(
        DateTimeOffset desde,
        DateTimeOffset hastaExclusivo,
        string? usuarioId,
        CancellationToken ct = default);
}
