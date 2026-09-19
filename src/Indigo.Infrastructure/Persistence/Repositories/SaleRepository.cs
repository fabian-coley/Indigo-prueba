using Indigo.Application.Abstractions.Persistence;
using Indigo.Application.Common;
using Indigo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Indigo.Infrastructure.Persistence.Repositories;

public sealed class SaleRepository : ISaleRepository
{
    private readonly AppDbContext _contexto;

    public SaleRepository(AppDbContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        _contexto = contexto;
    }

    public void Add(Sale venta) => _contexto.Sales.Add(venta);

    public Task<Sale?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _contexto.Sales
            .Include(venta => venta.Items)
            .FirstOrDefaultAsync(venta => venta.Id == id, ct);

    public async Task<PagedResult<VentaResumen>> GetPagedAsync(VentaFiltro filtro, CancellationToken ct = default)
    {
        var consulta = _contexto.Sales.AsNoTracking();

        if (!string.IsNullOrEmpty(filtro.UsuarioId))
        {
            consulta = consulta.Where(venta => venta.UsuarioId == filtro.UsuarioId);
        }

        var total = await consulta.CountAsync(ct);

        var items = await ProyectarResumen(
                consulta
                    .OrderByDescending(venta => venta.Fecha)
                    .ThenByDescending(venta => venta.Id)
                    .Skip((filtro.Page - 1) * filtro.PageSize)
                    .Take(filtro.PageSize))
            .ToListAsync(ct);

        return new PagedResult<VentaResumen>(items, filtro.Page, filtro.PageSize, total);
    }

    public async Task<IReadOnlyList<VentaResumen>> GetPorRangoAsync(
        DateTimeOffset desde,
        DateTimeOffset hastaExclusivo,
        string? usuarioId,
        CancellationToken ct = default)
    {
        var consulta = _contexto.Sales
            .AsNoTracking()
            .Where(venta => venta.Fecha >= desde && venta.Fecha < hastaExclusivo);

        if (!string.IsNullOrEmpty(usuarioId))
        {
            consulta = consulta.Where(venta => venta.UsuarioId == usuarioId);
        }

        return await ProyectarResumen(
                consulta
                    .OrderByDescending(venta => venta.Fecha)
                    .ThenByDescending(venta => venta.Id))
            .ToListAsync(ct);
    }

    private IQueryable<VentaResumen> ProyectarResumen(IQueryable<Sale> ventas) =>
        ventas.Join(
            _contexto.Users,
            venta => venta.UsuarioId,
            usuario => usuario.Id,
            (venta, usuario) => new VentaResumen(
                venta.Id,
                venta.Fecha,
                venta.Total,
                venta.Items.Sum(item => item.Cantidad),
                usuario.Email ?? string.Empty));
}
