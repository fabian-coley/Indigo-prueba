using Indigo.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Indigo.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _contexto;

    private IDbContextTransaction? _transaccion;

    public UnitOfWork(AppDbContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        _contexto = contexto;
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_transaccion is not null)
        {
            return;
        }

        _transaccion = await _contexto.Database.BeginTransactionAsync(ct);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _contexto.SaveChangesAsync(ct);

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaccion is null)
        {
            return;
        }

        await _transaccion.CommitAsync(ct);
        await LiberarTransaccionAsync();
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaccion is null)
        {
            return;
        }

        await _transaccion.RollbackAsync(ct);
        await LiberarTransaccionAsync();
    }

    private async Task LiberarTransaccionAsync()
    {
        var transaccion = _transaccion;
        _transaccion = null;

        await transaccion!.DisposeAsync();
    }
}
