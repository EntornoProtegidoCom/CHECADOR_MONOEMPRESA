using System.Net;
using System.Text.RegularExpressions;
using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Services;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChecadorWeb.Tests.Web;

public class ResumenHorasWebTests(ChecadorWebFactory factory) : IClassFixture<ChecadorWebFactory>
{
    private const string Ruta = "/ResumenH/ResumenHoras";

    [Theory]
    [InlineData(null, "")]
    [InlineData(null, "?handler=Excel")]
    [InlineData(2, "")]
    [InlineData(2, "?handler=Excel")]
    [InlineData(9, "")]
    [InlineData(9, "?handler=Excel")]
    public async Task Vista_y_descarga_requieren_marcajes_ver(int? perfil, string query)
    {
        using var cliente = factory.Cliente(perfil);
        var respuesta = await cliente.GetAsync(Ruta + query);
        Assert.Equal(perfil is null ? HttpStatusCode.Redirect : HttpStatusCode.Forbidden, respuesta.StatusCode);
        if (perfil is null) Assert.Contains("/Account/Login", respuesta.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Pagina_y_enlace_excel_conservan_filtros_y_muestran_totales_y_omisiones()
    {
        var servicio = new ResumenPrueba();
        using var app = ConServicio(servicio);
        using var cliente = Cliente(app);
        var html = await cliente.GetStringAsync(Ruta + "?Desde=2026-11-02&Hasta=2026-11-10&Trabajador=Jos%C3%A9%26Uno");
        Assert.Contains("INCOMPLETO", html);
        Assert.Contains("FALTA", html);
        Assert.Contains("Omisiones pendientes", html);
        Assert.Contains("TOTALES", html);
        Assert.Contains("Horas:minutos:segundos", html);
        Assert.Contains("08:00:00", html);
        Assert.Contains("07:59:56", html);
        Assert.Contains("31:29:56", html);
        Assert.Contains("table-success", html);
        Assert.Contains("table-warning", html);
        Assert.Contains("table-danger", html);
        Assert.Contains("aria-current=\"page\"", html);
        var enlace = Regex.Matches(html, "<a\\b[^>]*href=\"([^\"]+)\"[^>]*>")
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .Single(url => url.Contains("handler=Excel"));
        var parametros = QueryHelpers.ParseQuery(new Uri(cliente.BaseAddress!, enlace).Query);
        Assert.Equal("2026-11-02", parametros["Desde"].ToString());
        Assert.Equal("2026-11-10", parametros["Hasta"].ToString());
        Assert.Equal("José&Uno", parametros["Trabajador"].ToString());
        var respuesta = await cliente.GetAsync(enlace);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("José&Uno", servicio.UltimoFiltro!.Trabajador);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            respuesta.Content.Headers.ContentType!.MediaType);
        Assert.Contains("resumen_horas_2026-11-02_2026-11-10.xlsx",
            respuesta.Content.Headers.ContentDisposition!.ToString());
        Assert.True(respuesta.Headers.CacheControl!.NoStore);
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        using var paquete = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        Assert.Empty(new OpenXmlValidator().Validate(paquete));
        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheet("Resumen de Horas");
        Assert.Equal(13, hoja.LastColumnUsed()!.ColumnNumber());
        Assert.Equal(new DateTime(2026, 11, 2).ToString("ddd dd/MM/yyyy",
            System.Globalization.CultureInfo.GetCultureInfo("es-MX")), hoja.Cell(1, 3).GetString());
        Assert.Equal("Semana 02/11/2026 (hrs)", hoja.Cell(1, 10).GetString());
        Assert.Equal("Semana 09/11/2026 (hrs)", hoja.Cell(1, 11).GetString());
        Assert.Equal("Total del periodo (hrs)", hoja.Cell(1, 12).GetString());
        Assert.Equal(XLColor.FromHtml("#1F4E78"), hoja.Cell(1, 1).Style.Fill.BackgroundColor);
        Assert.Equal(XLColor.White, hoja.Cell(1, 1).Style.Font.FontColor);
        Assert.Equal(XLColor.FromHtml("#E2EFDA"), hoja.Cell(2, 3).Style.Fill.BackgroundColor);
        Assert.Equal(8.0 / 24, hoja.Cell(2, 3).GetTimeSpan().TotalDays, 12);
        Assert.Equal(XLDataType.TimeSpan, hoja.Cell(2, 3).DataType);
        Assert.Equal(FormatoDuracion.Excel, hoja.Cell(2, 3).Style.NumberFormat.Format);
        Assert.Equal(XLColor.FromHtml("#FFF2CC"), hoja.Cell(2, 4).Style.Fill.BackgroundColor);
        Assert.Equal("INCOMPLETO", hoja.Cell(2, 5).GetString());
        Assert.Equal("FALTA", hoja.Cell(2, 6).GetString());
        Assert.Equal(XLColor.FromHtml("#FCE4D6"), hoja.Cell(2, 5).Style.Fill.BackgroundColor);
        Assert.Equal(XLColor.FromHtml("#FCE4D6"), hoja.Cell(2, 6).Style.Fill.BackgroundColor);
        Assert.Equal(15.999 / 24, hoja.Cell(2, 10).GetTimeSpan().TotalDays, 12);
        Assert.Equal(7.5 / 24, hoja.Cell(2, 11).GetTimeSpan().TotalDays, 12);
        Assert.Equal(23.499 / 24, hoja.Cell(2, 12).GetTimeSpan().TotalDays, 12);
        Assert.Equal("Omisiones pendientes", hoja.Cell(2, 13).GetString());
        Assert.Equal("TOTALES", hoja.Cell(4, 2).GetString());
        Assert.Equal(16.0 / 24, hoja.Cell(4, 3).GetTimeSpan().TotalDays, 12);
        Assert.Equal(23.999 / 24, hoja.Cell(4, 10).GetTimeSpan().TotalDays, 12);
        Assert.Equal(31.499 / 24, hoja.Cell(4, 12).GetTimeSpan().TotalDays, 12);
        Assert.Equal("31:29:56", hoja.Cell(4, 12).GetFormattedString());
        Assert.Equal(XLColor.FromHtml("#D9D9D9"), hoja.Cell(4, 3).Style.Fill.BackgroundColor);
        Assert.True(hoja.Cell(4, 3).Style.Font.Bold);
        Assert.Equal("=SUM(1,2)", hoja.Cell(3, 2).GetString());
        Assert.False(hoja.Cell(3, 2).HasFormula);
        Assert.Equal(1, hoja.SheetView.SplitRow);
        Assert.Equal(2, hoja.SheetView.SplitColumn);
        Assert.True(hoja.AutoFilter.IsEnabled);
    }

    [Fact]
    public async Task Ejemplo_de_marcajes_y_totales_mayores_a_24_horas_se_muestran_y_se_suman_en_excel()
    {
        var inicio = new DateOnly(2026, 11, 2);
        var fin = new DateOnly(2026, 11, 9);
        var fechas = new ReporteHorasResultado(inicio, fin, []).Fechas;
        var empleado = new ResumenHorasEmpleadoDto { IdTrabajador = 14, NombreTrabajador = "Empleado 14" };
        var otro = new ResumenHorasEmpleadoDto { IdTrabajador = 15, NombreTrabajador = "Empleado 15" };
        foreach (var fecha in fechas)
        {
            empleado.Dias[fecha] = CalculadorJornada.Calcular([]);
            otro.Dias[fecha] = CalculadorJornada.Calcular([]);
        }
        var dia = fechas[0];
        empleado.Dias[dia] = CalculadorJornada.Calcular(
        [
            new(dia.AddHours(18).AddMinutes(34).AddSeconds(14), 1),
            new(dia.AddHours(18).AddMinutes(47).AddSeconds(20), 2),
            new(dia.AddHours(19).AddMinutes(51).AddSeconds(8), 3),
            new(dia.AddHours(21).AddMinutes(21).AddSeconds(57), 4),
        ]);
        empleado.Dias[fechas[^1]] = new() { HorasEfectivas = 23.5 };
        otro.Dias[dia] = new() { HorasEfectivas = 23 };
        var datos = new ReporteHorasResultado(inicio, fin, [empleado, otro]);
        using var app = ConServicio(new ResumenPrueba { Datos = datos });
        using var cliente = Cliente(app);
        var query = "?Desde=2026-11-02&Hasta=2026-11-09";
        var html = await cliente.GetStringAsync(Ruta + query);
        Assert.Contains("01:43:55", html);
        Assert.Contains("25:13:55", html);
        Assert.Contains("24:43:55", html);
        Assert.Contains("48:13:55", html);
        Assert.DoesNotContain("Horas decimales", html);

        var bytes = await cliente.GetByteArrayAsync(Ruta + query + "&handler=Excel");
        using var paquete = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        var hojaXml = paquete.WorkbookPart!.WorksheetParts.Single().Worksheet;
        var valorXml = hojaXml.Descendants<Cell>().Single(c => c.CellReference == "C2");
        Assert.True(valorXml.DataType is null || valorXml.DataType == CellValues.Number);
        Assert.Equal((1 * 3600 + 43 * 60 + 55) / 86400.0,
            double.Parse(valorXml.CellValue!.InnerText, System.Globalization.CultureInfo.InvariantCulture), 12);
        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheet(1);
        Assert.Equal("01:43:55", hoja.Cell(2, 3).GetFormattedString());
        Assert.Equal("01:43:55", hoja.Cell(2, 9).GetFormattedString());
        Assert.Equal("23:30:00", hoja.Cell(2, 10).GetFormattedString());
        Assert.Equal("25:13:55", hoja.Cell(2, 11).GetFormattedString());
        Assert.Equal("24:43:55", hoja.Cell(4, 3).GetFormattedString());
        Assert.Equal("48:13:55", hoja.Cell(4, 11).GetFormattedString());
        Assert.Equal(XLDataType.TimeSpan, hoja.Cell(2, 3).DataType);
        Assert.Equal(FormatoDuracion.Excel, hoja.Cell(4, 11).Style.NumberFormat.Format);
        Assert.Equal((1 * 3600 + 43 * 60 + 55) / 86400.0, hoja.Cell(2, 3).GetTimeSpan().TotalDays, 12);
        hoja.Cell(5, 3).FormulaA1 = "SUM(C2:C3)";
        Assert.Equal(hoja.Cell(4, 3).GetTimeSpan().TotalDays, hoja.Cell(5, 3).GetDouble(), 12);
    }

    [Theory]
    [InlineData("Desde=no-es-fecha&Hasta=2026-11-10")]
    [InlineData("Desde=2026-11-10&Hasta=2026-11-02")]
    [InlineData("Desde=2024-01-01&Hasta=2025-01-01")]
    [InlineData("Desde=0001-01-01&Hasta=0001-01-02")]
    [InlineData("Desde=2026-11-07&Hasta=2026-11-08")]
    [InlineData("Desde=2026-11-07&Hasta=2026-11-07")]
    [InlineData("Desde=2026-11-08&Hasta=2026-11-08")]
    [InlineData("Trabajador=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task Filtros_invalidos_muestran_error_y_no_consultan_ni_descargan(string filtros)
    {
        var servicio = new ResumenPrueba();
        using var app = ConServicio(servicio);
        using var cliente = Cliente(app);
        foreach (var handler in new[] { "", "&handler=Excel" })
        {
            var respuesta = await cliente.GetAsync($"{Ruta}?{filtros}{handler}");
            Assert.Equal(handler.Length == 0 ? HttpStatusCode.OK : HttpStatusCode.BadRequest, respuesta.StatusCode);
            var html = await respuesta.Content.ReadAsStringAsync();
            Assert.Contains("validation-summary-errors", html);
            Assert.DoesNotContain("Descargar Excel", html);
        }
        Assert.Equal(0, servicio.Consultas);
    }

    [Fact]
    public async Task Error_de_consulta_es_visible_y_no_descarga_excel_vacio()
    {
        using var app = ConServicio(new ResumenPrueba { Fallar = true });
        using var cliente = Cliente(app);
        var pagina = await cliente.GetStringAsync(Ruta);
        Assert.Contains("No fue posible consultar", pagina);
        Assert.DoesNotContain("Descargar Excel", pagina);
        var respuesta = await cliente.GetAsync(Ruta + "?handler=Excel");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Contains("No fue posible consultar", await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("text/html", respuesta.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Sin_empleados_muestra_estado_vacio_y_excel_con_encabezado_y_totales_cero()
    {
        var servicio = new ResumenPrueba { SinEmpleados = true };
        using var app = ConServicio(servicio);
        using var cliente = Cliente(app);
        var pagina = await cliente.GetStringAsync(Ruta);
        Assert.Contains("No hay empleados activos", pagina);
        var bytes = await cliente.GetByteArrayAsync(Ruta + "?handler=Excel");
        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheet(1);
        Assert.Equal(2, hoja.LastRowUsed()!.RowNumber());
        Assert.Equal("TOTALES", hoja.Cell(2, 2).GetString());
        Assert.Equal(TimeSpan.Zero, hoja.Cell(2, 3).GetTimeSpan());
        Assert.NotNull(servicio.UltimoFiltro!.Desde);
        Assert.NotNull(servicio.UltimoFiltro.Hasta);
    }

    [Fact]
    public async Task Menu_oculta_opcion_sin_permiso_y_la_muestra_con_permiso()
    {
        using var app = ConServicio(new ResumenPrueba { Fallar = true });
        using var permitido = Cliente(app);
        Assert.Contains("href=\"/ResumenH/ResumenHoras\"", await permitido.GetStringAsync(Ruta));
        using var denegado = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        denegado.DefaultRequestHeaders.Add(ChecadorWebFactory.HeaderPerfil, "9");
        var respuesta = await denegado.GetAsync("/Account/AccesoDenegado");
        Assert.DoesNotContain("href=\"/ResumenH/ResumenHoras\"", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Fin_de_semana_no_aparece_en_pagina_excel_totales_ni_estatus()
    {
        var lunes = new DateTime(2026, 11, 2);
        var empleado = new ResumenHorasEmpleadoDto { IdTrabajador = 14, NombreTrabajador = "Empleado 14" };
        for (var i = 0; i < 5; i++)
            empleado.Dias[lunes.AddDays(i)] = new() { HorasEfectivas = 8 };
        empleado.Dias[lunes.AddDays(5)] = new() { HorasEfectivas = 20 };
        empleado.Dias[lunes.AddDays(6)] = new() { EsIncompleto = true, Estatus = "INCOMPLETO" };
        var datos = new ReporteHorasResultado(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8), [empleado]);
        using var app = ConServicio(new ResumenPrueba { Datos = datos });
        using var cliente = Cliente(app);
        var query = "?Desde=2026-11-02&Hasta=2026-11-08";
        var html = await cliente.GetStringAsync(Ruta + query);
        var encabezados = Regex.Match(html, "<thead\\b[^>]*>(.*?)</thead>", RegexOptions.Singleline).Value;
        Assert.Equal(9, Regex.Matches(encabezados, "<th\\s").Count);
        foreach (var fecha in new[] { "02/11/2026", "03/11/2026", "04/11/2026", "05/11/2026", "06/11/2026" })
            Assert.Contains(fecha, encabezados);
        Assert.DoesNotContain("07/11/2026", encabezados);
        Assert.DoesNotContain("08/11/2026", encabezados);
        var cuerpo = Regex.Match(html, "<tbody\\b[^>]*>(.*?)</tbody>", RegexOptions.Singleline).Value;
        Assert.Contains("40:00:00", cuerpo);
        Assert.Contains("<td>OK</td>", cuerpo);
        Assert.DoesNotContain("INCOMPLETO", cuerpo);
        Assert.DoesNotContain("Omisiones pendientes", cuerpo);
        var totales = Regex.Match(html, "<tfoot\\b[^>]*>(.*?)</tfoot>", RegexOptions.Singleline).Value;
        Assert.Contains("40:00:00", totales);

        var bytes = await cliente.GetByteArrayAsync(Ruta + query + "&handler=Excel");
        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheet(1);
        Assert.Equal(9, hoja.LastColumnUsed()!.ColumnNumber());
        Assert.DoesNotContain(hoja.Row(1).CellsUsed(), c => c.GetString().Contains("07/11/2026") || c.GetString().Contains("08/11/2026"));
        Assert.Equal(40.0 / 24, hoja.Cell(2, 8).GetTimeSpan().TotalDays, 12);
        Assert.Equal(40.0 / 24, hoja.Cell(3, 8).GetTimeSpan().TotalDays, 12);
        Assert.Equal("OK", hoja.Cell(2, 9).GetString());
    }

    private WebApplicationFactory<Program> ConServicio(ResumenPrueba servicio) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IResumenHorasService>();
            services.AddSingleton<IResumenHorasService>(servicio);
        }));

    private static HttpClient Cliente(WebApplicationFactory<Program> app)
    {
        var cliente = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost"),
        });
        cliente.DefaultRequestHeaders.Add(ChecadorWebFactory.HeaderPerfil, "4");
        cliente.DefaultRequestHeaders.Add(ChecadorWebFactory.HeaderUsuario, "prueba.resumen");
        return cliente;
    }

    private sealed class ResumenPrueba : IResumenHorasService
    {
        public ResumenHorasFiltro? UltimoFiltro { get; private set; }
        public int Consultas { get; private set; }
        public bool Fallar { get; init; }
        public bool SinEmpleados { get; init; }
        public ReporteHorasResultado? Datos { get; init; }

        public Task<ReporteHorasResultado> ObtenerAsync(ResumenHorasFiltro filtro, CancellationToken ct = default)
        {
            Consultas++;
            UltimoFiltro = filtro;
            if (Fallar) throw new InvalidOperationException("Fallo de prueba");
            if (Datos is not null) return Task.FromResult(Datos);
            var reporte = new ReporteHorasResultado(filtro.Desde!.Value, filtro.Hasta!.Value, []);
            if (SinEmpleados) return Task.FromResult(reporte);
            var fechas = reporte.Fechas;
            var primero = new ResumenHorasEmpleadoDto { IdTrabajador = 1, NombreTrabajador = "José&Uno" };
            var segundo = new ResumenHorasEmpleadoDto { IdTrabajador = 2, NombreTrabajador = "=SUM(1,2)" };
            foreach (var fecha in fechas)
            {
                primero.Dias[fecha] = CalculadorJornada.Calcular([]);
                segundo.Dias[fecha] = CalculadorJornada.Calcular([]);
            }
            primero.Dias[fechas[0]] = new() { HorasEfectivas = 8 };
            segundo.Dias[fechas[0]] = new() { HorasEfectivas = 8 };
            if (fechas.Count > 1) primero.Dias[fechas[1]] = new() { HorasEfectivas = 7.999 };
            if (fechas.Count > 2) primero.Dias[fechas[2]] = new() { EsIncompleto = true, Estatus = "INCOMPLETO" };
            var siguienteSemana = fechas[0].AddDays(7);
            if (primero.Dias.ContainsKey(siguienteSemana)) primero.Dias[siguienteSemana] = new() { HorasEfectivas = 7.5 };
            return Task.FromResult(reporte with { Empleados = [primero, segundo] });
        }
    }
}
