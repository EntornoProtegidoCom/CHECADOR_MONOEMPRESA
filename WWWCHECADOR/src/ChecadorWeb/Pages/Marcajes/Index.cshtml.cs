using System.Globalization;
using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChecadorWeb.Pages.Marcajes;

[Authorize(Policy = Permisos.MarcajesVer)]
public class IndexModel(IMarcajeService marcajes, IAuditoria auditoria) : PaginaBase
{
    public MarcajeFiltro Filtro { get; private set; } = new();
    public PaginaResultado<MarcajeFila>? Resultado { get; private set; }
    public PaginadorVista? Paginador { get; private set; }

    public async Task OnGetAsync([FromQuery] MarcajeFiltro filtro, CancellationToken ct)
    {
        Filtro = filtro;
        Resultado = await CargarAsync(() => marcajes.BuscarAsync(filtro, ct));
        if (Resultado is not null)
            Paginador = PaginadorVista.De(Resultado, new Dictionary<string, string?>
            {
                ["Desde"] = filtro.Desde?.ToString("yyyy-MM-dd"),
                ["Hasta"] = filtro.Hasta?.ToString("yyyy-MM-dd"),
                ["Trabajador"] = filtro.Trabajador,
                ["Tipo"] = filtro.Tipo?.ToString(),
                ["Equipo"] = filtro.Equipo,
            });
    }

    public async Task<IActionResult> OnGetFotoAsync(int id, CancellationToken ct)
    {
        if (!await AutorizarAsync(Permisos.MarcajesFoto)) return Forbid();

        var foto = await marcajes.ObtenerFotoAsync(id, ct);
        if (foto is null || foto.Length == 0) return NotFound();

        var tipo = DetectarTipoImagen(foto);
        if (tipo is null) return NotFound();

        await auditoria.RegistrarAsync(new EventoAuditoria("VER_FOTO", "MM_HORARIO", id.ToString(), true), ct);
        Response.Headers.CacheControl = "no-store";
        return File(foto, tipo);
    }

    public async Task<IActionResult> OnGetExcelAsync([FromQuery] MarcajeFiltro filtro, CancellationToken ct)
    {
        Filtro = filtro;
        Response.Headers.CacheControl = "no-store";
        var filas = await CargarAsync(() => marcajes.ExportarAsync(filtro, ct));
        if (filas is null)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Page();
        }

        using var archivo = new MemoryStream();
        using (var documento = SpreadsheetDocument.Create(archivo, SpreadsheetDocumentType.Workbook))
        {
            var libro = documento.AddWorkbookPart();
            libro.Workbook = new Workbook();
            var hoja = libro.AddNewPart<WorksheetPart>();
            using (var escritor = OpenXmlWriter.Create(hoja))
            {
                escritor.WriteStartElement(new Worksheet());
                escritor.WriteStartElement(new SheetData());
                EscribirFila(escritor, ["IdTrabajador", "Empleado", "Fecha", "Hora", "Tipo", "Equipo", "Correo", "Foto"]);
                foreach (var m in filas)
                {
                    ct.ThrowIfCancellationRequested();
                    EscribirFila(escritor,
                    [
                        m.IdTrabajador?.ToString(CultureInfo.InvariantCulture) ?? "",
                        m.NombreTrabajador ?? "",
                        m.Hora.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                        m.Hora.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                        m.TipoNombre,
                        m.Equipo ?? "—",
                        m.Enviado == true ? "Enviado" : $"Pendiente ({m.Intentos ?? 0} intentos)",
                        m.TieneFoto ? "Con foto" : "—",
                    ]);
                }
                escritor.WriteEndElement();
                escritor.WriteEndElement();
            }
            libro.Workbook.AppendChild(new Sheets(new Sheet
            {
                Id = libro.GetIdOfPart(hoja), SheetId = 1U, Name = "Marcajes",
            }));
            libro.Workbook.Save();
        }

        return File(archivo.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"marcajes_{filtro.Desde:yyyy-MM-dd}_{filtro.Hasta:yyyy-MM-dd}.xlsx");
    }

    private static void EscribirFila(OpenXmlWriter escritor, string[] valores)
    {
        escritor.WriteStartElement(new Row());
        foreach (var valor in valores)
        {
            escritor.WriteElement(new Cell(new InlineString(new Text(valor)
            {
                Space = SpaceProcessingModeValues.Preserve,
            }))
            {
                DataType = CellValues.InlineString,
            });
        }
        escritor.WriteEndElement();
    }

    internal static string? DetectarTipoImagen(byte[] datos)
    {
        if (datos.Length >= 3 && datos[0] == 0xFF && datos[1] == 0xD8 && datos[2] == 0xFF) return "image/jpeg";
        if (datos.Length >= 8 && datos[0] == 0x89 && datos[1] == 0x50 && datos[2] == 0x4E && datos[3] == 0x47) return "image/png";
        if (datos.Length >= 2 && datos[0] == 0x42 && datos[1] == 0x4D) return "image/bmp";
        return null;
    }
}
