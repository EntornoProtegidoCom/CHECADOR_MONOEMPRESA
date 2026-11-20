using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChecadorWeb.Pages.ResumenH;

[Authorize(Policy = Permisos.MarcajesVer)]
public class ResumenHorasModel(IResumenHorasService resumen) : PaginaBase
{
    public ResumenHorasFiltro Filtro { get; private set; } = new();
    public ReporteHorasResultado? Resultado { get; private set; }

    public async Task OnGetAsync([FromQuery] ResumenHorasFiltro filtro, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Preparar(filtro)) return;
        Resultado = await CargarAsync(() => resumen.ObtenerAsync(filtro, ct));
    }

    public async Task<IActionResult> OnGetExcelAsync([FromQuery] ResumenHorasFiltro filtro, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Preparar(filtro))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Page();
        }
        var archivo = await CargarAsync(async () =>
        {
            var reporte = await resumen.ObtenerAsync(filtro, ct);
            return ResumenHorasExcel.Generar(reporte, ct);
        });
        if (archivo is null)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Page();
        }
        return File(archivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"resumen_horas_{filtro.Desde:yyyy-MM-dd}_{filtro.Hasta:yyyy-MM-dd}.xlsx");
    }

    private bool Preparar(ResumenHorasFiltro filtro)
    {
        Filtro = filtro;
        filtro.AplicarPredeterminados();
        TryValidateModel(filtro);
        return ModelState.IsValid;
    }
}
