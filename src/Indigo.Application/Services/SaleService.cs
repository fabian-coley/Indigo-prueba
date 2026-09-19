using Indigo.Application.Abstractions.Persistence;
using Indigo.Application.Abstractions.Security;
using Indigo.Application.Common;
using Indigo.Application.Contracts.Ventas;
using Indigo.Application.Mapping;
using Indigo.Domain.Entities;
using Indigo.Domain.Exceptions;

namespace Indigo.Application.Services;

public class SaleService
{
    private readonly ISaleRepository _ventas;
    private readonly IProductRepository _productos;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _usuarioActual;
    private readonly IIdentityService _identity;

    public SaleService(
        ISaleRepository ventas,
        IProductRepository productos,
        IUnitOfWork unitOfWork,
        ICurrentUserService usuarioActual,
        IIdentityService identity)
    {
        _ventas = ventas;
        _productos = productos;
        _unitOfWork = unitOfWork;
        _usuarioActual = usuarioActual;
        _identity = identity;
    }

    public async Task<VentaDetalleDto> RegistrarAsync(RegistrarVentaRequest request, CancellationToken ct = default)
    {
        var usuarioId = _usuarioActual.UsuarioIdRequerido();
        var venta = new Sale(usuarioId, DateTimeOffset.UtcNow);

        await _unitOfWork.BeginTransactionAsync(ct);

        try
        {
            foreach (var item in request.Items)
            {
                var producto = await _productos.GetByIdAsync(item.ProductoId, incluirInactivos: true, ct: ct)
                    ?? throw new ExcepcionReglaDeNegocio($"No existe el producto con id '{item.ProductoId}'.");

                venta.AgregarItem(producto, item.Cantidad);
            }

            venta.ValidarParaRegistrar();

            _ventas.Add(venta);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        var email = await _identity.ObtenerEmailAsync(usuarioId, ct) ?? string.Empty;

        return venta.ToDetalle(email);
    }

    public async Task<VentaDetalleDto?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default)
    {
        var venta = await _ventas.GetByIdAsync(id, ct);

        if (venta is null)
        {
            return null;
        }

        if (!_usuarioActual.EsAdmin && venta.UsuarioId != _usuarioActual.UsuarioId)
        {
            return null;
        }

        var email = await _identity.ObtenerEmailAsync(venta.UsuarioId, ct) ?? string.Empty;

        return venta.ToDetalle(email);
    }

    public async Task<PagedResult<VentaDto>> ListarAsync(int page, int pageSize, CancellationToken ct = default)
    {
        Paginacion.Validar(page, pageSize);

        var filtro = new VentaFiltro(
            _usuarioActual.EsAdmin ? null : _usuarioActual.UsuarioIdRequerido(),
            page,
            pageSize);

        var pagina = await _ventas.GetPagedAsync(filtro, ct);

        return pagina.ToDto();
    }
}
