using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Indigo.Api.IntegrationTests.Infraestructura;
using Indigo.Application.Contracts.Reportes;
using Indigo.Domain.Entities;
using Indigo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Indigo.Api.IntegrationTests;

public sealed class ReportesTests : IClassFixture<IndigoApiFactory>
{
    private const decimal PrecioUnitario = 8.75m;

    private readonly IndigoApiFactory _fabrica;

    public ReportesTests(IndigoApiFactory fabrica) => _fabrica = fabrica;


    [Fact]
    public async Task Ventas_ConFromPosteriorATo_Devuelve400()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.GetAsync("/api/reports/sales?from=2026-03-20&to=2026-03-10");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be("La fecha 'from' no puede ser posterior a la fecha 'to'.");
    }

    [Fact]
    public async Task Ventas_ConRangoMayorA366Dias_Devuelve400()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.GetAsync("/api/reports/sales?from=2020-01-01&to=2021-01-02");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(OpcionesJson.Valor);

        problema!.Detail.Should().Be("El rango del reporte no puede superar los 366 días.");
    }

    [Fact]
    public async Task Ventas_ConRangoDeExactamente366Dias_Devuelve200()
    {
        var cliente = await _fabrica.ClienteAdminAsync();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var respuesta = await cliente.GetAsync(
            $"/api/reports/sales?from={hoy.AddDays(-365):yyyy-MM-dd}&to={hoy:yyyy-MM-dd}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ventas_SinNingunaFecha_Devuelve400YSeñalaFrom()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.GetAsync("/api/reports/sales");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ValidationProblemDetails>(OpcionesJson.Valor);

        problema!.Errors.Should().ContainKey("from");
        problema.Errors["from"].Should().ContainSingle()
            .Which.Should().Be("Las fechas 'from' y 'to' son obligatorias, en formato YYYY-MM-DD.");
    }

    [Fact]
    public async Task Ventas_SoloConFrom_Devuelve400YSeñalaTo()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.GetAsync("/api/reports/sales?from=2026-03-10");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problema = await respuesta.Content.ReadFromJsonAsync<ValidationProblemDetails>(OpcionesJson.Valor);

        problema!.Errors.Should().ContainKey("to");
        problema.Errors.Should().NotContainKey("from");
    }

    [Fact]
    public async Task Ventas_ConFechaMalFormada_Devuelve400()
    {
        var cliente = await _fabrica.ClienteAdminAsync();

        var respuesta = await cliente.GetAsync("/api/reports/sales?from=2026-13-45&to=2026-03-20");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }


    [Fact]
    public async Task Ventas_ConDatosConocidos_DevuelveLosTotalesDelDia()
    {
        var admin = await _fabrica.ClienteAdminAsync();
        var fecha = new DateOnly(2026, 3, 10);
        var usuarioId = await _fabrica.BuscarUsuarioIdAsync(DatosSembrados.EmailAdmin);

        await SembrarVentaAsync(usuarioId, fecha, cantidad: 3);
        await SembrarVentaAsync(usuarioId, fecha, cantidad: 1);

        var reporte = await LeerReporteAsync(admin, fecha, fecha);

        reporte.Desde.Should().Be(fecha);
        reporte.Hasta.Should().Be(fecha);
        reporte.TotalVentas.Should().Be(2);
        reporte.TotalItems.Should().Be(4);
        reporte.TotalGeneral.Should().Be(PrecioUnitario * 4);

        var dia = reporte.PorDia.Should().ContainSingle().Subject;

        dia.Fecha.Should().Be(fecha);
        dia.Ventas.Should().Be(2);
        dia.Items.Should().Be(4);
        dia.Total.Should().Be(PrecioUnitario * 4);
    }

    [Fact]
    public async Task Ventas_ConRangoDeVariosDias_AgrupaPorDiaYOmiteLosVacios()
    {
        var admin = await _fabrica.ClienteAdminAsync();
        var usuarioId = await _fabrica.BuscarUsuarioIdAsync(DatosSembrados.EmailAdmin);

        var primerDia = new DateOnly(2026, 4, 6);
        var segundoDia = new DateOnly(2026, 4, 8);

        await SembrarVentaAsync(usuarioId, primerDia, cantidad: 2);
        await SembrarVentaAsync(usuarioId, segundoDia, cantidad: 5);

        var reporte = await LeerReporteAsync(admin, primerDia, segundoDia);

        reporte.TotalVentas.Should().Be(2);
        reporte.TotalItems.Should().Be(7);
        reporte.TotalGeneral.Should().Be(PrecioUnitario * 7);

        reporte.PorDia.Should().HaveCount(2);

        reporte.PorDia.Select(dia => dia.Fecha).Should().Equal(primerDia, segundoDia);
        reporte.PorDia.Select(dia => dia.Items).Should().Equal(2, 5);
    }

    [Fact]
    public async Task Ventas_EnUnRangoSinVentas_DevuelveTodoEnCero()
    {
        var admin = await _fabrica.ClienteAdminAsync();
        var fecha = new DateOnly(2020, 1, 15);

        var reporte = await LeerReporteAsync(admin, fecha, fecha);

        reporte.TotalVentas.Should().Be(0);
        reporte.TotalItems.Should().Be(0);
        reporte.TotalGeneral.Should().Be(0m);
        reporte.PorDia.Should().BeEmpty("los días sin ventas no se rellenan con ceros");
    }

    [Fact]
    public async Task Ventas_SembradasFueraDelRango_NoSeCuentan()
    {
        var admin = await _fabrica.ClienteAdminAsync();
        var usuarioId = await _fabrica.BuscarUsuarioIdAsync(DatosSembrados.EmailAdmin);

        var dentro = new DateOnly(2026, 5, 4);
        var fuera = new DateOnly(2026, 5, 6);

        await SembrarVentaAsync(usuarioId, dentro, cantidad: 1);
        await SembrarVentaAsync(usuarioId, fuera, cantidad: 9);

        var reporte = await LeerReporteAsync(admin, dentro, dentro);

        reporte.TotalVentas.Should().Be(1);
        reporte.TotalItems.Should().Be(1);
        reporte.PorDia.Should().ContainSingle();
        reporte.PorDia[0].Fecha.Should().Be(dentro);
    }

    [Fact]
    public async Task Ventas_ComoVendedor_SoloSumaLasPropias()
    {
        var admin = await _fabrica.ClienteAdminAsync();
        var (vendedorA, emailA) = await _fabrica.ClienteVendedorNuevoAsync();
        var (vendedorB, emailB) = await _fabrica.ClienteVendedorNuevoAsync();

        var fecha = new DateOnly(2026, 6, 15);

        await SembrarVentaAsync(await _fabrica.BuscarUsuarioIdAsync(emailA), fecha, cantidad: 2);
        await SembrarVentaAsync(await _fabrica.BuscarUsuarioIdAsync(emailB), fecha, cantidad: 3);

        var reporteA = await LeerReporteAsync(vendedorA, fecha, fecha);

        reporteA.TotalVentas.Should().Be(1);
        reporteA.TotalItems.Should().Be(2);
        reporteA.TotalGeneral.Should().Be(PrecioUnitario * 2);

        var reporteB = await LeerReporteAsync(vendedorB, fecha, fecha);

        reporteB.TotalVentas.Should().Be(1);
        reporteB.TotalItems.Should().Be(3);
        reporteB.TotalGeneral.Should().Be(PrecioUnitario * 3);

        var reporteAdmin = await LeerReporteAsync(admin, fecha, fecha);

        reporteAdmin.TotalVentas.Should().Be(2);
        reporteAdmin.TotalItems.Should().Be(5);
        reporteAdmin.TotalGeneral.Should().Be(PrecioUnitario * 5);
    }


    private async Task SembrarVentaAsync(string usuarioId, DateOnly fecha, int cantidad)
    {
        await _fabrica.EnAlcanceAsync(async servicios =>
        {
            var contexto = servicios.GetRequiredService<AppDbContext>();
            var producto = await contexto.Products.SingleAsync(p => p.Nombre == DatosSembrados.ProductoAbundante);

            var venta = new Sale(usuarioId, new DateTimeOffset(fecha.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));

            venta.AgregarItem(producto, cantidad);
            venta.ValidarParaRegistrar();

            contexto.Sales.Add(venta);
            await contexto.SaveChangesAsync();

            return true;
        });
    }

    private static async Task<ReporteVentasDto> LeerReporteAsync(HttpClient cliente, DateOnly desde, DateOnly hasta)
    {
        var respuesta = await cliente.GetAsync(
            $"/api/reports/sales?from={desde:yyyy-MM-dd}&to={hasta:yyyy-MM-dd}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await respuesta.Content.ReadFromJsonAsync<ReporteVentasDto>(OpcionesJson.Valor))!;
    }
}
