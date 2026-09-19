using Indigo.Application.Common;
using Indigo.Domain.Entities;

namespace Indigo.Application.Abstractions.Persistence;

public interface IProductRepository
{
    Task<PagedResult<Product>> GetPagedAsync(ProductoFiltro filtro, CancellationToken ct = default);

    Task<Product?> GetByIdAsync(Guid id, bool incluirInactivos = false, CancellationToken ct = default);

    void Add(Product producto);

    void Update(Product producto);

    void Delete(Product producto);
}
