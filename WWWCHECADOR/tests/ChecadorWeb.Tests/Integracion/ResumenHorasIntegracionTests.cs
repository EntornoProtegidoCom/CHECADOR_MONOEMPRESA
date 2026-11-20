using ChecadorWeb.Models;
using ChecadorWeb.Services;
using ChecadorWeb.Tests.Web;
using Microsoft.EntityFrameworkCore;

namespace ChecadorWeb.Tests.Integracion;

public class ResumenHorasIntegracionTests
{
    [SkippableFact]
    public async Task Ultima_fecha_soportada_incluye_activos_sin_marcajes_como_falta()
    {
        Skip.IfNot(ChecadorWebFactory.HayBaseDatos, "Sin acceso a la base de datos de pruebas.");
        await using var c = await ContextoRevertido.CrearAsync();
        var servicio = new ResumenHorasService(c.Db);
        var reporte = await servicio.ObtenerAsync(new ResumenHorasFiltro
        {
            Desde = DateOnly.MaxValue, Hasta = DateOnly.MaxValue,
        });
        var activos = await c.Db.MM_TRABAJADOREs.CountAsync(t => t.status == true);
        Assert.Equal(activos, reporte.Empleados.Count);
        foreach (var empleado in reporte.Empleados)
        {
            var dia = Assert.Single(empleado.Dias).Value;
            Assert.Equal("FALTA", dia.Estatus);
            Assert.True(empleado.TieneOmisiones);
            Assert.Equal(0, empleado.TotalHorasSemanales);
        }
    }

    [SkippableFact]
    public async Task Consulta_incluye_activos_sin_marcajes_y_respeta_fechas_y_filtros()
    {
        Skip.IfNot(ChecadorWebFactory.HayBaseDatos, "Sin acceso a la base de datos de pruebas.");
        await using var c = await ContextoRevertido.CrearAsync();
        var servicio = new ResumenHorasService(c.Db);
        var desde = new DateOnly(2026, 11, 2);
        var hasta = desde.AddDays(8);
        var reporte = await servicio.ObtenerAsync(new ResumenHorasFiltro { Desde = desde, Hasta = hasta });
        var activos = await c.Db.MM_TRABAJADOREs.Where(t => t.status == true).Select(t => t.IDTrabajador).ToListAsync();
        Assert.Equal(activos.Order(), reporte.Empleados.Select(e => e.IdTrabajador).Order());
        Assert.Equal(7, reporte.Fechas.Count);
        Assert.Equal(2, reporte.Semanas.Count);
        foreach (var empleado in reporte.Empleados)
        {
            Assert.Equal(7, empleado.Dias.Count);
            Assert.DoesNotContain(empleado.Dias.Keys, d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
            var inicio = desde.ToDateTime(TimeOnly.MinValue);
            var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var registros = await c.Db.MM_HORARIOs
                .Where(h => h.IDTrabajador == empleado.IdTrabajador && h.Horaregistro >= inicio && h.Horaregistro < fin)
                .Select(h => new MarcajeHora(h.Horaregistro, h.TipoRegistro)).ToListAsync();
            foreach (var fecha in reporte.Fechas)
            {
                var esperado = CalculadorJornada.Calcular(registros.Where(h => h.Hora.Date == fecha));
                var actual = empleado.Dias[fecha];
                Assert.Equal(esperado.Estatus, actual.Estatus);
                Assert.Equal(esperado.HorasEfectivas, actual.HorasEfectivas);
                Assert.Equal(esperado.TiempoComida, actual.TiempoComida);
            }
            var totalEsperado = reporte.Fechas.Select(fecha =>
                CalculadorJornada.Calcular(registros.Where(h => h.Hora.Date == fecha))).Sum(d => d.HorasEfectivas);
            Assert.Equal(totalEsperado, empleado.TotalHorasSemanales, 10);
        }
        if (reporte.Empleados.FirstOrDefault() is { } primero)
        {
            var porId = await servicio.ObtenerAsync(new ResumenHorasFiltro
            {
                Desde = desde, Hasta = hasta, Trabajador = primero.IdTrabajador.ToString(),
            });
            Assert.Equal(primero.IdTrabajador, Assert.Single(porId.Empleados).IdTrabajador);
            var nombre = await c.Db.MM_TRABAJADOREs.Where(t => t.IDTrabajador == primero.IdTrabajador)
                .Select(t => t.Nombre).SingleAsync();
            var porNombre = await servicio.ObtenerAsync(new ResumenHorasFiltro
            {
                Desde = desde, Hasta = hasta, Trabajador = nombre,
            });
            Assert.Contains(porNombre.Empleados, e => e.IdTrabajador == primero.IdTrabajador);
        }
    }
}
