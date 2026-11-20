using System.Globalization;
using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ClosedXML.Excel;

namespace ChecadorWeb.Services;

public static class ResumenHorasExcel
{
    public static byte[] Generar(ReporteHorasResultado reporte, CancellationToken ct = default)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Resumen de Horas");
        var cultura = CultureInfo.GetCultureInfo("es-MX");
        var fechas = reporte.Fechas;
        var semanas = reporte.Semanas.Count > 1 ? reporte.Semanas : Array.Empty<DateTime>();
        var totalCol = fechas.Count + semanas.Count + 3;
        var estatusCol = totalCol + 1;
        hoja.Cell(1, 1).Value = "ID Empleado";
        hoja.Cell(1, 2).Value = "Nombre del Colaborador";
        for (var i = 0; i < fechas.Count; i++)
            hoja.Cell(1, i + 3).Value = fechas[i].ToString("ddd dd/MM/yyyy", cultura);
        for (var i = 0; i < semanas.Count; i++)
            hoja.Cell(1, fechas.Count + i + 3).Value = $"Semana {semanas[i]:dd/MM/yyyy} (hrs)";
        hoja.Cell(1, totalCol).Value = semanas.Count == 0 ? "Total semanal (hrs)" : "Total del periodo (hrs)";
        hoja.Cell(1, estatusCol).Value = "Estatus del Periodo";
        var encabezado = hoja.Range(1, 1, 1, estatusCol);
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Font.FontColor = XLColor.White;
        encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");

        var subtotalesSemana = semanas.ToDictionary(s => s, _ => 0.0);
        var fila = 2;
        foreach (var empleado in reporte.Empleados)
        {
            ct.ThrowIfCancellationRequested();
            hoja.Cell(fila, 1).Value = empleado.IdTrabajador;
            hoja.Cell(fila, 2).Value = empleado.NombreTrabajador;
            for (var i = 0; i < fechas.Count; i++)
            {
                var detalle = empleado.Dias[fechas[i]];
                var celda = hoja.Cell(fila, i + 3);
                if (detalle.EsIncompleto)
                {
                    celda.Value = detalle.Estatus;
                    celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#FCE4D6");
                    celda.Style.Font.FontColor = XLColor.DarkRed;
                }
                else
                {
                    celda.Value = FormatoDuracion.DiasParaExcel(detalle.HorasEfectivas);
                    celda.Style.NumberFormat.Format = FormatoDuracion.Excel;
                    celda.Style.Fill.BackgroundColor = XLColor.FromHtml(
                        detalle.HorasEfectivas >= 8 ? "#E2EFDA" : "#FFF2CC");
                }
            }
            var totalesSemana = empleado.TotalesPorSemana;
            for (var i = 0; i < semanas.Count; i++)
            {
                hoja.Cell(fila, fechas.Count + i + 3).Value = FormatoDuracion.DiasParaExcel(totalesSemana[semanas[i]]);
                subtotalesSemana[semanas[i]] += totalesSemana[semanas[i]];
            }
            hoja.Cell(fila, totalCol).Value = FormatoDuracion.DiasParaExcel(empleado.TotalHorasSemanales);
            hoja.Cell(fila, totalCol).Style.Font.Bold = true;
            hoja.Range(fila, fechas.Count + 3, fila, totalCol).Style.NumberFormat.Format = FormatoDuracion.Excel;
            hoja.Cell(fila, estatusCol).Value = empleado.TieneOmisiones ? "Omisiones pendientes" : "OK";
            hoja.Cell(fila, estatusCol).Style.Font.FontColor = empleado.TieneOmisiones ? XLColor.DarkRed : XLColor.DarkGreen;
            fila++;
        }

        hoja.Cell(fila, 2).Value = "TOTALES";
        for (var i = 0; i < fechas.Count; i++)
            hoja.Cell(fila, i + 3).Value = FormatoDuracion.DiasParaExcel(reporte.Empleados
                .Where(e => !e.Dias[fechas[i]].EsIncompleto).Sum(e => e.Dias[fechas[i]].HorasEfectivas));
        for (var i = 0; i < semanas.Count; i++)
            hoja.Cell(fila, fechas.Count + i + 3).Value = FormatoDuracion.DiasParaExcel(subtotalesSemana[semanas[i]]);
        hoja.Cell(fila, totalCol).Value = FormatoDuracion.DiasParaExcel(reporte.Empleados.Sum(e => e.TotalHorasSemanales));
        var totales = hoja.Range(fila, 1, fila, estatusCol);
        totales.Style.Font.Bold = true;
        totales.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
        hoja.Range(fila, 3, fila, totalCol).Style.NumberFormat.Format = FormatoDuracion.Excel;
        hoja.Range(1, 1, fila - 1, estatusCol).SetAutoFilter();
        hoja.SheetView.FreezeRows(1);
        hoja.SheetView.FreezeColumns(2);
        hoja.Columns(1, estatusCol).AdjustToContents(10, 45);
        hoja.Range(1, 1, 1, estatusCol).Style.Alignment.WrapText = true;
        hoja.Row(1).Height = 32;

        using var archivo = new MemoryStream();
        libro.SaveAs(archivo);
        return archivo.ToArray();
    }
}
