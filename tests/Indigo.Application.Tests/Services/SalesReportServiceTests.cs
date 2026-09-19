using FluentAssertions;
using Indigo.Application.Abstractions.Persistence;
using Indigo.Application.Abstractions.Security;
using Indigo.Application.Common;
using Indigo.Application.Services;
using Indigo.Domain.Constants;
using Indigo.Domain.Exceptions;
using Moq;

namespace Indigo.Application.Tests.Services;

public class SalesReportServiceTests
{
    private const string AdminId = "admin-1";
    private const string VendedorId = "vendedor-1";

    private readonly Mock<ISaleRepository> _ventas = new();

    private SalesReportService CrearServicio(ICurrentUserService usuarioActual) => new(_ventas.Object, usuarioActual);

    private static ICurrentUserService Usuario(string? usuarioId, bool esAdmin)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(u => u.UsuarioId).Returns(usuarioId);
        mock.SetupGet(u => u.EstaAutenticado).Returns(usuarioId is not null);
        mock.SetupGet(u => u.EsAdmin).Returns(esAdmin);
        mock.SetupGet(u => u.Roles).Returns(esAdmin ? [Roles.Admin] : [Roles.Vendedor]);
        return mock.Object;
    }

    private void DevolverVentas(params VentaResumen[] resumenes) =>
        _ventas.Setup(v => v.GetPorRangoAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resumenes);

    [Fact]
    public async Task GenerarAsync_ConDesdePosteriorAHasta_LanzaSinConsultarElRepositorio()
    {
        var act = () => CrearServicio(Usuario(AdminId, esAdmin: true))
            .GenerarAsync(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 1));

        await act.Should().ThrowAsync<ExcepcionReglaDeNegocio>()
            .WithMessage("*'from'*'to'*");

        _ventas.Verify(v => v.GetPorRangoAsync(
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerarAsync_ConRangoDe366Dias_LoSigueAceptando()
    {
        DevolverVentas();

        var desde = new DateOnly(2026, 1, 1);
        var reporte = await CrearServicio(Usuario(AdminId, esAdmin: true)).GenerarAsync(desde, desde.AddDays(365));

        reporte.TotalVentas.Should().Be(0);
    }

    [Fact]
    public async Task GenerarAsync_ConRangoDe367Dias_LanzaSinConsultarElRepositorio()
    {
        var desde = new DateOnly(2026, 1, 1);

        var act = () => CrearServicio(Usuario(AdminId, esAdmin: true)).GenerarAsync(desde, desde.AddDays(366));

        await act.Should().ThrowAsync<ExcepcionReglaDeNegocio>()
            .WithMessage("*366*");

        _ventas.Verify(v => v.GetPorRangoAsync(
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerarAsync_ConsultaElRangoEnUtcConLimiteExclusivoEnElDiaSiguiente()
    {
        DevolverVentas();

        await CrearServicio(Usuario(AdminId, esAdmin: true))
            .GenerarAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));

        _ventas.Verify(v => v.GetPorRangoAsync(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 11, 0, 0, 0, TimeSpan.Zero),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerarAsync_AgrupaPorDiaSoloLosDiasConVentas()
    {
        DevolverVentas(
            new VentaResumen(Guid.NewGuid(), new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), 100m, 2, "a@indigo.com"),
            new VentaResumen(Guid.NewGuid(), new DateTimeOffset(2026, 1, 1, 18, 30, 0, TimeSpan.Zero), 50.5m, 1, "a@indigo.com"),
            new VentaResumen(Guid.NewGuid(), new DateTimeOffset(2026, 1, 3, 12, 0, 0, TimeSpan.Zero), 25m, 5, "b@indigo.com"));

        var reporte = await CrearServicio(Usuario(AdminId, esAdmin: true))
            .GenerarAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3));

        reporte.Desde.Should().Be(new DateOnly(2026, 1, 1));
        reporte.Hasta.Should().Be(new DateOnly(2026, 1, 3));
        reporte.TotalGeneral.Should().Be(175.5m);
        reporte.TotalVentas.Should().Be(3);
        reporte.TotalItems.Should().Be(8);

        reporte.PorDia.Should().HaveCount(2);
        reporte.PorDia[0].Fecha.Should().Be(new DateOnly(2026, 1, 1));
        reporte.PorDia[0].Ventas.Should().Be(2);
        reporte.PorDia[0].Items.Should().Be(3);
        reporte.PorDia[0].Total.Should().Be(150.5m);
        reporte.PorDia[1].Fecha.Should().Be(new DateOnly(2026, 1, 3));
        reporte.PorDia[1].Total.Should().Be(25m);
    }

    [Fact]
    public async Task GenerarAsync_AgrupaPorDiaUtcNoPorElOffsetDeLaVenta()
    {
        DevolverVentas(
            new VentaResumen(Guid.NewGuid(), new DateTimeOffset(2026, 1, 1, 23, 0, 0, TimeSpan.FromHours(-5)), 10m, 1, "a@indigo.com"),
            new VentaResumen(Guid.NewGuid(), new DateTimeOffset(2026, 1, 2, 1, 0, 0, TimeSpan.FromHours(-5)), 20m, 1, "a@indigo.com"));

        var reporte = await CrearServicio(Usuario(AdminId, esAdmin: true))
            .GenerarAsync(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 2));

        reporte.PorDia.Should().ContainSingle();
        reporte.PorDia[0].Fecha.Should().Be(new DateOnly(2026, 1, 2));
        reporte.PorDia[0].Ventas.Should().Be(2);
        reporte.PorDia[0].Total.Should().Be(30m);
    }

    [Fact]
    public async Task GenerarAsync_SinVentas_DevuelveTodoEnCeroYListaVacia()
    {
        DevolverVentas();

        var reporte = await CrearServicio(Usuario(AdminId, esAdmin: true))
            .GenerarAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));

        reporte.TotalGeneral.Should().Be(0m);
        reporte.TotalVentas.Should().Be(0);
        reporte.TotalItems.Should().Be(0);
        reporte.PorDia.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerarAsync_ComoAdmin_NoFiltraPorUsuario()
    {
        DevolverVentas();

        await CrearServicio(Usuario(AdminId, esAdmin: true)).GenerarAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));

        _ventas.Verify(v => v.GetPorRangoAsync(
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            It.Is<string?>(usuarioId => usuarioId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerarAsync_ComoVendedor_FiltraPorSuPropioId()
    {
        DevolverVentas();

        await CrearServicio(Usuario(VendedorId, esAdmin: false)).GenerarAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));

        _ventas.Verify(v => v.GetPorRangoAsync(
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            VendedorId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerarAsync_SinUsuarioAutenticado_LanzaSinConsultarElRepositorio()
    {
        var act = () => CrearServicio(Usuario(null, esAdmin: false))
            .GenerarAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));

        await act.Should().ThrowAsync<InvalidOperationException>();
        _ventas.Verify(v => v.GetPorRangoAsync(
            It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
