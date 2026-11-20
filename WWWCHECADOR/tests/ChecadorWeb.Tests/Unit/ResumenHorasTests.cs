using System.ComponentModel.DataAnnotations;
using ChecadorWeb.Data;
using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Services;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace ChecadorWeb.Tests.Unit;

public class ResumenHorasTests
{
    private static readonly DateTime Dia = new(2026, 11, 2);

    [Fact]
    public void Cuatro_marcajes_descuentan_comida_y_ordenan_por_hora()
    {
        var detalle = CalculadorJornada.Calcular(
        [
            new(Dia.AddHours(17), 4), new(Dia.AddHours(8), 1),
            new(Dia.AddHours(14), 3), new(Dia.AddHours(13), 2),
        ]);
        Assert.False(detalle.EsIncompleto);
        Assert.Equal("OK", detalle.Estatus);
        Assert.Equal(8, detalle.HorasEfectivas);
        Assert.Equal(TimeSpan.FromHours(1), detalle.TiempoComida);
    }

    [Fact]
    public void Jornada_directa_conserva_minutos_y_segundos_sin_redondear_antes_de_sumar()
    {
        var detalle = CalculadorJornada.Calcular(
            [new(Dia.AddHours(8), 1), new(Dia.AddHours(15).AddMinutes(30).AddSeconds(30), 4)]);
        Assert.False(detalle.EsIncompleto);
        Assert.Null(detalle.TiempoComida);
        Assert.Equal(7.5 + 30.0 / 3600, detalle.HorasEfectivas, 10);
    }

    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(1.97, "01:58:12")]
    [InlineData(8.25, "08:15:00")]
    [InlineData(24, "24:00:00")]
    [InlineData(100.5, "100:30:00")]
    [InlineData(7.999, "07:59:56")]
    public void Duraciones_se_muestran_en_horas_minutos_y_segundos(double horas, string esperado)
    {
        Assert.Equal(esperado, FormatoDuracion.Texto(horas));
    }

    [Fact]
    public void Ejemplo_de_cuatro_marcajes_produce_una_hora_cuarenta_y_tres_minutos_y_cincuenta_y_cinco_segundos()
    {
        var detalle = CalculadorJornada.Calcular(
        [
            new(Dia.AddHours(18).AddMinutes(34).AddSeconds(14), 1),
            new(Dia.AddHours(18).AddMinutes(47).AddSeconds(20), 2),
            new(Dia.AddHours(19).AddMinutes(51).AddSeconds(8), 3),
            new(Dia.AddHours(21).AddMinutes(21).AddSeconds(57), 4),
        ]);
        Assert.Equal("01:43:55", FormatoDuracion.Texto(detalle.HorasEfectivas));
        Assert.Equal(TimeSpan.FromMinutes(63).Add(TimeSpan.FromSeconds(48)), detalle.TiempoComida);
    }

    [Theory]
    [InlineData(new short[] { 1 })]
    [InlineData(new short[] { 4 })]
    [InlineData(new short[] { 1, 2 })]
    [InlineData(new short[] { 1, 3, 4 })]
    [InlineData(new short[] { 1, 2, 4 })]
    [InlineData(new short[] { 1, 3, 2, 4 })]
    [InlineData(new short[] { 4, 1 })]
    [InlineData(new short[] { 1, 1, 4 })]
    [InlineData(new short[] { 1, 2, 3, 4, 4 })]
    [InlineData(new short[] { 1, 2, 3, 9 })]
    [InlineData(new short[] { 1, 4, 1, 4 })]
    public void Omisiones_duplicados_y_tipos_desordenados_o_desconocidos_no_suman(short[] tipos)
    {
        var detalle = CalculadorJornada.Calcular(tipos.Select((t, i) => new MarcajeHora(Dia.AddHours(8 + i), t)));
        Assert.True(detalle.EsIncompleto);
        Assert.Equal("INCOMPLETO", detalle.Estatus);
        Assert.Equal(0, detalle.HorasEfectivas);
        Assert.Null(detalle.TiempoComida);
    }

    [Fact]
    public void Tiempos_iguales_o_cruces_de_medianoche_son_incompletos()
    {
        foreach (var salida in new[] { Dia.AddHours(8), Dia.AddDays(1).AddHours(1) })
        {
            var detalle = CalculadorJornada.Calcular([new(Dia.AddHours(8), 1), new(salida, 4)]);
            Assert.True(detalle.EsIncompleto);
            Assert.Equal(0, detalle.HorasEfectivas);
        }
    }

    [Fact]
    public void Sin_marcajes_es_falta_y_cuenta_como_omision()
    {
        var detalle = CalculadorJornada.Calcular([]);
        Assert.Equal("FALTA", detalle.Estatus);
        Assert.True(detalle.EsIncompleto);
        Assert.Equal(0, detalle.HorasEfectivas);
        var empleado = new ResumenHorasEmpleadoDto { Dias = { [Dia] = detalle } };
        Assert.True(empleado.TieneOmisiones);
    }

    [Fact]
    public void Totales_separan_semanas_en_cambio_de_ano_y_excluyen_omisiones()
    {
        var empleado = new ResumenHorasEmpleadoDto
        {
            Dias =
            {
                [new DateTime(2026, 12, 31)] = new() { HorasEfectivas = 8 },
                [new DateTime(2027, 1, 1)] = new() { HorasEfectivas = 7.5 },
                [new DateTime(2027, 1, 4)] = new() { HorasEfectivas = 6 },
                [new DateTime(2027, 1, 5)] = new() { HorasEfectivas = 99, EsIncompleto = true },
            },
        };
        Assert.Equal(21.5, empleado.TotalHorasSemanales);
        Assert.Equal(15.5, empleado.TotalesPorSemana[new DateTime(2026, 12, 28)]);
        Assert.Equal(6, empleado.TotalesPorSemana[new DateTime(2027, 1, 4)]);
        Assert.True(empleado.TieneOmisiones);
    }

    [Fact]
    public void Predeterminado_es_semana_actual_de_lunes_a_viernes()
    {
        var filtro = new ResumenHorasFiltro();
        filtro.AplicarPredeterminados();
        Assert.Equal(DayOfWeek.Monday, filtro.Desde!.Value.DayOfWeek);
        Assert.Equal(filtro.Desde.Value.AddDays(4), filtro.Hasta);
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        Assert.InRange(hoy, filtro.Desde.Value, filtro.Desde.Value.AddDays(6));
    }

    [Theory]
    [InlineData("2024-01-01", "2024-12-31", true)]
    [InlineData("2024-01-01", "2025-01-01", false)]
    [InlineData("2026-11-02", "2026-11-01", false)]
    [InlineData("2026-11-02", "2026-11-02", true)]
    [InlineData("0001-01-01", "0001-01-02", false)]
    [InlineData("1753-01-01", "1753-01-01", true)]
    [InlineData("9999-12-31", "9999-12-31", true)]
    [InlineData("2026-11-07", "2026-11-07", false)]
    [InlineData("2026-11-08", "2026-11-08", false)]
    [InlineData("2026-11-07", "2026-11-08", false)]
    [InlineData("2026-11-07", "2026-11-09", true)]
    [InlineData("2026-11-06", "2026-11-08", true)]
    [InlineData("2026-11-08", "2026-11-14", true)]
    public void Rango_es_inclusivo_y_rechaza_invertidos_y_excesivos(string desde, string hasta, bool valido)
    {
        var filtro = new ResumenHorasFiltro { Desde = DateOnly.Parse(desde), Hasta = DateOnly.Parse(hasta) };
        var errores = new List<ValidationResult>();
        Assert.Equal(valido, Validator.TryValidateObject(filtro, new ValidationContext(filtro), errores, true));
    }

    [Theory]
    [InlineData("2026-11-02", "2026-11-01")]
    [InlineData("2026-11-07", "2026-11-08")]
    public async Task Servicio_rechaza_rango_invalido_antes_de_acceder_a_bd(string desde, string hasta)
    {
        await using var db = new ChecadorDbContext(new DbContextOptionsBuilder<ChecadorDbContext>().Options);
        var servicio = new ResumenHorasService(db);
        await Assert.ThrowsAsync<ValidationException>(() => servicio.ObtenerAsync(new ResumenHorasFiltro
        {
            Desde = DateOnly.Parse(desde), Hasta = DateOnly.Parse(hasta),
        }));
    }

    [Fact]
    public void Excel_de_jornada_valida_sin_omisiones_muestra_ok_y_totales_identicos()
    {
        var empleado = new ResumenHorasEmpleadoDto
        {
            IdTrabajador = 1, NombreTrabajador = "Colaborador",
            Dias = { [Dia] = new() { HorasEfectivas = 8.25 } },
        };
        var fecha = DateOnly.FromDateTime(Dia);
        var reporte = new ReporteHorasResultado(fecha, fecha, [empleado]);
        using var libro = new XLWorkbook(new MemoryStream(ResumenHorasExcel.Generar(reporte)));
        var hoja = libro.Worksheet(1);
        Assert.False(empleado.TieneOmisiones);
        Assert.Equal(5, hoja.LastColumnUsed()!.ColumnNumber());
        Assert.Equal("Total semanal (hrs)", hoja.Cell(1, 4).GetString());
        Assert.Equal("OK", hoja.Cell(2, 5).GetString());
        Assert.Equal(XLColor.DarkGreen, hoja.Cell(2, 5).Style.Font.FontColor);
        foreach (var fila in new[] { 2, 3 })
            foreach (var columna in new[] { 3, 4 })
            {
                var celda = hoja.Cell(fila, columna);
                Assert.Equal(8.25 / 24, celda.GetTimeSpan().TotalDays, 12);
                Assert.Equal(FormatoDuracion.Excel, celda.Style.NumberFormat.Format);
                Assert.Equal("08:15:00", celda.GetFormattedString());
            }
    }

    [Fact]
    public void Excel_de_una_semana_tiene_cinco_dias_y_nueve_columnas()
    {
        var reporte = new ReporteHorasResultado(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8), []);
        using var libro = new XLWorkbook(new MemoryStream(ResumenHorasExcel.Generar(reporte)));
        var hoja = libro.Worksheet(1);
        Assert.Equal(9, hoja.LastColumnUsed()!.ColumnNumber());
        Assert.Contains("06/11/2026", hoja.Cell(1, 7).GetString());
        Assert.Equal("Total semanal (hrs)", hoja.Cell(1, 8).GetString());
        Assert.Equal("Estatus del Periodo", hoja.Cell(1, 9).GetString());
    }

    [Fact]
    public void Las_sumas_conservan_fracciones_de_segundo_antes_de_redondear_para_mostrar()
    {
        var duracion = TimeSpan.FromMilliseconds(400).TotalHours;
        var empleado = new ResumenHorasEmpleadoDto
        {
            IdTrabajador = 1, NombreTrabajador = "Colaborador",
            Dias =
            {
                [Dia] = new() { HorasEfectivas = duracion },
                [Dia.AddDays(1)] = new() { HorasEfectivas = duracion },
            },
        };
        Assert.Equal("00:00:00", FormatoDuracion.Texto(duracion));
        Assert.Equal("00:00:01", FormatoDuracion.Texto(empleado.TotalHorasSemanales));
        var reporte = new ReporteHorasResultado(DateOnly.FromDateTime(Dia), DateOnly.FromDateTime(Dia.AddDays(1)), [empleado]);
        using var libro = new XLWorkbook(new MemoryStream(ResumenHorasExcel.Generar(reporte)));
        var hoja = libro.Worksheet(1);
        Assert.Equal(TimeSpan.FromMilliseconds(400).TotalDays, hoja.Cell(2, 3).GetTimeSpan().TotalDays, 12);
        Assert.Equal(TimeSpan.FromMilliseconds(800).TotalDays, hoja.Cell(2, 5).GetTimeSpan().TotalDays, 12);
        Assert.Equal(TimeSpan.FromMilliseconds(800).TotalDays, hoja.Cell(3, 5).GetTimeSpan().TotalDays, 12);
    }

    [Fact]
    public void Excel_admite_los_366_dias_inclusivos_sin_truncar_columnas()
    {
        var reporte = new ReporteHorasResultado(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), []);
        Assert.Equal(262, reporte.Fechas.Count);
        Assert.Equal(53, reporte.Semanas.Count);
        using var libro = new XLWorkbook(new MemoryStream(ResumenHorasExcel.Generar(reporte)));
        var hoja = libro.Worksheet(1);
        Assert.Equal(319, hoja.LastColumnUsed()!.ColumnNumber());
        Assert.Contains("31/12/2024", hoja.Cell(1, 264).GetString());
        Assert.Equal("Total del periodo (hrs)", hoja.Cell(1, 318).GetString());
        Assert.Equal(TimeSpan.Zero, hoja.Cell(2, 318).GetTimeSpan());
    }

    [Theory]
    [InlineData("2026-11-02", "2026-11-08", 5)]
    [InlineData("2026-11-07", "2026-11-13", 5)]
    [InlineData("2026-11-08", "2026-11-09", 1)]
    [InlineData("2026-11-06", "2026-11-08", 1)]
    [InlineData("2026-11-07", "2026-11-08", 0)]
    [InlineData("2024-02-26", "2024-03-03", 5)]
    [InlineData("2026-12-31", "2027-01-04", 3)]
    public void Fechas_del_reporte_solo_contienen_lunes_a_viernes(string desde, string hasta, int dias)
    {
        var reporte = new ReporteHorasResultado(DateOnly.Parse(desde), DateOnly.Parse(hasta), []);
        Assert.Equal(dias, reporte.Fechas.Count);
        Assert.All(reporte.Fechas, fecha =>
        {
            Assert.NotEqual(DayOfWeek.Saturday, fecha.DayOfWeek);
            Assert.NotEqual(DayOfWeek.Sunday, fecha.DayOfWeek);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fin_de_semana_no_suma_horas_ni_genera_omisiones(bool conMarcajes)
    {
        var empleado = new ResumenHorasEmpleadoDto();
        for (var i = 0; i < 5; i++)
            empleado.Dias[Dia.AddDays(i)] = new() { HorasEfectivas = 8 };
        empleado.Dias[Dia.AddDays(5)] = conMarcajes
            ? new() { HorasEfectivas = 20 }
            : CalculadorJornada.Calcular([]);
        empleado.Dias[Dia.AddDays(6)] = new() { EsIncompleto = true, Estatus = "INCOMPLETO" };

        Assert.Equal(40, empleado.TotalHorasSemanales);
        Assert.Equal(40, Assert.Single(empleado.TotalesPorSemana).Value);
        Assert.False(empleado.TieneOmisiones);

        empleado.Dias[Dia.AddDays(7)] = CalculadorJornada.Calcular([]);
        Assert.True(empleado.TieneOmisiones);
        Assert.Equal(40, empleado.TotalHorasSemanales);
    }
}
