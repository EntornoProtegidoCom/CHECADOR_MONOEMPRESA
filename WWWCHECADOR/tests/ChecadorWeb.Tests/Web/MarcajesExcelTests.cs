using System.Net;
using System.Text.RegularExpressions;
using ChecadorWeb.Models;
using ChecadorWeb.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChecadorWeb.Tests.Web;

public class MarcajesExcelTests(ChecadorWebFactory factory) : IClassFixture<ChecadorWebFactory>
{
    private static readonly string[] Encabezados =
        ["IdTrabajador", "Empleado", "Fecha", "Hora", "Tipo", "Equipo", "Correo", "Foto"];

    [Fact]
    public async Task Descarga_incluye_todas_las_filas_y_conserva_los_filtros_aplicados()
    {
        var servicio = new MarcajesPrueba();
        using var app = ConServicio(servicio);
        using var cliente = Cliente(app);
        var html = await cliente.GetStringAsync(
            "/Marcajes?Desde=2026-10-01&Hasta=2026-10-31&Trabajador=Jos%C3%A9&Tipo=1&Equipo=PC%26Uno&Pagina=2&Tamano=10");
        var enlace = Regex.Matches(html, "<a\\b[^>]*href=\"([^\"]+)\"[^>]*>")
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .Single(url => url.Contains("handler=Excel"));
        var parametros = QueryHelpers.ParseQuery(new Uri(cliente.BaseAddress!, enlace).Query);
        Assert.Equal("2026-10-01", parametros["Desde"].ToString());
        Assert.Equal("2026-10-31", parametros["Hasta"].ToString());
        Assert.Equal("José", parametros["Trabajador"].ToString());
        Assert.Equal("1", parametros["Tipo"].ToString());
        Assert.Equal("PC&Uno", parametros["Equipo"].ToString());
        Assert.False(parametros.ContainsKey("Pagina"));
        Assert.False(parametros.ContainsKey("Tamano"));

        var respuesta = await cliente.GetAsync(enlace);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            respuesta.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", respuesta.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Contains("marcajes_2026-10-01_2026-10-31.xlsx",
            respuesta.Content.Headers.ContentDisposition.ToString());
        Assert.True(respuesta.Headers.CacheControl!.NoStore);
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        var filas = LeerFilas(bytes);
        Assert.Equal(26, filas.Length);
        Assert.Equal(Encabezados, filas[0]);
        Assert.All(filas, fila => Assert.Equal(8, fila.Length));
        Assert.Equal(new[] { "25", "José", "01/10/2026", "00:00:00", "Entrada", "PC&Uno", "Pendiente (0 intentos)", "—" },
            filas[^1]);
        Assert.Equal(new DateOnly(2026, 10, 1), servicio.FiltroExportado!.Desde);
        Assert.Equal(new DateOnly(2026, 10, 31), servicio.FiltroExportado.Hasta);
        Assert.Equal("José", servicio.FiltroExportado!.Trabajador);
        Assert.Equal("PC&Uno", servicio.FiltroExportado.Equipo);
        Assert.Equal((short)1, servicio.FiltroExportado.Tipo);
    }

    [Fact]
    public async Task Excel_conserva_textos_especiales_y_formulas_como_texto_sin_exportar_imagenes()
    {
        var servicio = new MarcajesPrueba
        {
            Filas =
            [
                new(1, new DateTime(2026, 10, 1, 8, 5, 6), 1, 7, "José, \"Pérez\"\r\nLópez",
                    " =SUM(1,2)", "\t+123", true, false, 2),
                new(2, new DateTime(2026, 10, 1), 4, null, null, null, null, false, true, null),
            ],
        };
        using var app = ConServicio(servicio);
        using var cliente = Cliente(app);
        var respuesta = await cliente.GetAsync("/Marcajes?handler=Excel");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var filas = LeerFilas(await respuesta.Content.ReadAsByteArrayAsync());
        Assert.Equal(3, filas.Length);
        Assert.Equal(Encabezados, filas[0]);
        Assert.Equal(new[] { "7", "José, \"Pérez\"\nLópez", "01/10/2026", "08:05:06",
            "Entrada", " =SUM(1,2)", "Pendiente (2 intentos)", "Con foto" }, filas[1]);
        Assert.Equal(new[] { "", "", "01/10/2026", "00:00:00", "Salida", "—", "Enviado", "—" }, filas[2]);
    }

    [Theory]
    [InlineData("=SUM(1,2)")]
    [InlineData("+123")]
    [InlineData("-123")]
    [InlineData("@SUM(1,2)")]
    [InlineData("\t+123")]
    [InlineData("<equipo>&\"uno\"")]
    public async Task Excel_no_interpreta_el_contenido_de_las_celdas(string texto)
    {
        using var app = ConServicio(new MarcajesPrueba
        {
            Filas = [new(1, new DateTime(2026, 10, 1), 1, 7, texto, texto, null, false, null, null)],
        });
        using var cliente = Cliente(app);
        var respuesta = await cliente.GetAsync("/Marcajes?handler=Excel");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var filas = LeerFilas(await respuesta.Content.ReadAsByteArrayAsync());
        Assert.Equal(texto, filas[1][1]);
        Assert.Equal(texto, filas[1][5]);
    }

    [Fact]
    public async Task Sin_resultados_descarga_solo_encabezados()
    {
        using var app = ConServicio(new MarcajesPrueba { Filas = [] });
        using var cliente = Cliente(app);
        var respuesta = await cliente.GetAsync("/Marcajes?handler=Excel");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var filas = LeerFilas(await respuesta.Content.ReadAsByteArrayAsync());
        Assert.Equal(Encabezados, Assert.Single(filas));
    }

    [Fact]
    public async Task Vista_separa_fecha_hora_e_identificador_y_no_muestra_ip()
    {
        using var app = ConServicio(new MarcajesPrueba
        {
            Filas =
            [
                new(1, new DateTime(2026, 10, 1, 8, 5, 6), 1, 7, "Nombre Completo",
                    "PC", "192.0.2.123", false, true, null),
            ],
        });
        using var cliente = Cliente(app);
        var html = await cliente.GetStringAsync("/Marcajes");
        var encabezados = Regex.Matches(html, "<th scope=\"col\">([^<]+)</th>")
            .Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(new[] { "IdTrabajador", "Empleado", "Fecha", "Hora", "Tipo", "Equipo", "Correo", "Foto" },
            encabezados);
        Assert.Contains("<td data-label=\"Fecha\">01/10/2026</td>", html);
        Assert.Contains("<td data-label=\"Hora\">08:05:06</td>", html);
        Assert.Contains("<td data-label=\"IdTrabajador\">7</td>", html);
        Assert.Contains("<td data-label=\"Empleado\">Nombre Completo</td>", html);
        Assert.DoesNotContain("data-label=\"IP\"", html);
        Assert.DoesNotContain("192.0.2.123", html);
    }

    [Fact]
    public async Task Error_de_consulta_muestra_mensaje_y_no_descarga_un_excel_incompleto()
    {
        using var app = ConServicio(new MarcajesPrueba { Fallar = true });
        using var cliente = Cliente(app);
        var respuesta = await cliente.GetAsync("/Marcajes?handler=Excel");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Null(respuesta.Content.Headers.ContentDisposition);
        Assert.Contains("No fue posible consultar", await respuesta.Content.ReadAsStringAsync());
    }

    private static string[][] LeerFilas(byte[] bytes)
    {
        using var archivo = new MemoryStream(bytes);
        using var documento = SpreadsheetDocument.Open(archivo, false);
        Assert.Empty(new OpenXmlValidator().Validate(documento));
        var libro = documento.WorkbookPart!;
        var hoja = Assert.Single(libro.Workbook.Sheets!.Elements<Sheet>());
        Assert.Equal("Marcajes", hoja.Name!.Value);
        var contenido = (WorksheetPart)libro.GetPartById(hoja.Id!.Value!);
        Assert.Empty(contenido.ImageParts);
        Assert.Null(contenido.DrawingsPart);
        Assert.Empty(libro.ExternalWorkbookParts);
        Assert.Empty(contenido.Worksheet.Descendants<CellFormula>());
        var filas = contenido.Worksheet.GetFirstChild<SheetData>()!.Elements<Row>().ToArray();
        return filas.Select(fila => fila.Elements<Cell>().Select(celda =>
        {
            Assert.Equal(CellValues.InlineString, celda.DataType!.Value);
            return celda.InlineString!.InnerText;
        }).ToArray()).ToArray();
    }

    private WebApplicationFactory<Program> ConServicio(MarcajesPrueba servicio) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMarcajeService>();
            services.AddSingleton<IMarcajeService>(servicio);
        }));

    private static HttpClient Cliente(WebApplicationFactory<Program> app)
    {
        var cliente = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false,
        });
        cliente.DefaultRequestHeaders.Add(ChecadorWebFactory.HeaderPerfil, "4");
        cliente.DefaultRequestHeaders.Add(ChecadorWebFactory.HeaderUsuario, "prueba.excel");
        return cliente;
    }

    private sealed class MarcajesPrueba : IMarcajeService
    {
        public List<MarcajeFila> Filas { get; init; } = Enumerable.Range(1, 25)
            .Select(i => new MarcajeFila(i, new DateTime(2026, 10, 1), 1, i, "José",
                "PC&Uno", null, false, null, null)).ToList();
        public bool Fallar { get; init; }
        public MarcajeFiltro? FiltroExportado { get; private set; }

        public Task<PaginaResultado<MarcajeFila>> BuscarAsync(MarcajeFiltro filtro, CancellationToken ct = default)
        {
            var pagina = Paginacion.NormalizarPagina(filtro.Pagina);
            var tamano = Paginacion.NormalizarTamano(filtro.Tamano);
            return Task.FromResult(new PaginaResultado<MarcajeFila>
            {
                Elementos = Filas.Skip((pagina - 1) * tamano).Take(tamano).ToList(),
                Pagina = pagina, TamanoPagina = tamano, Total = Filas.Count,
            });
        }

        public Task<List<MarcajeFila>> ExportarAsync(MarcajeFiltro filtro, CancellationToken ct = default)
        {
            if (Fallar) throw new InvalidOperationException("Fallo de prueba");
            FiltroExportado = filtro;
            return Task.FromResult(Filas);
        }

        public Task<byte[]?> ObtenerFotoAsync(int id, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
