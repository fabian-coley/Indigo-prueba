using Indigo.Application.Abstractions.Persistence;
using Indigo.Application.Common;
using Indigo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Indigo.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly AppDbContext _contexto;

    public ProductRepository(AppDbContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        _contexto = contexto;
    }

    public async Task<PagedResult<Product>> GetPagedAsync(ProductoFiltro filtro, CancellationToken ct = default)
    {
        var consulta = _contexto.Products
            .AsNoTracking()
            .Where(producto => producto.Activo);

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var termino = filtro.Search.Trim().ToLower();

            consulta = consulta.Where(producto => producto.Nombre.ToLower().Contains(termino));
        }

        if (filtro.Categoria is { } categoria)
        {
            consulta = consulta.Where(producto => producto.Categoria == categoria);
        }

        var total = await consulta.CountAsync(ct);

        var items = await consulta
            .OrderBy(producto => EF.Functions.Collate(producto.Nombre, "NOCASE"))
            .ThenBy(producto => producto.Id)
            .Skip((filtro.Page - 1) * filtro.PageSize)
            .Take(filtro.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Product>(items, filtro.Page, filtro.PageSize, total);
    }

    public Task<Product?> GetByIdAsync(Guid id, bool incluirInactivos = false, CancellationToken ct = default) =>
        _contexto.Products.FirstOrDefaultAsync(
            producto => producto.Id == id && (incluirInactivos || producto.Activo),
            ct);

    public void Add(Product producto) => _contexto.Products.Add(producto);

    public void Update(Product producto) => _contexto.Products.Update(producto);

    public void Delete(Product producto)
    {
        _contexto.Products.Update(producto);
    }
}
