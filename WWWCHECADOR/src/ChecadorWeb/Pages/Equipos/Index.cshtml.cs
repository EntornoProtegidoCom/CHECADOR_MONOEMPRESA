using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChecadorWeb.Pages.Equipos;

[Authorize(Policy = Permisos.EquiposVer)]
public class IndexModel(IVinculoService vinculos) : PaginaBase
{
    public VinculoFiltro Filtro { get; private set; } = new();
    public PaginaResultado<VinculoFila>? Resultado { get; private set; }
    public PaginadorVista? Paginador { get; private set; }
    public IReadOnlyList<ConflictoVinculo>? Conflictos { get; private set; }
    public string? ErrorConflictos { get; private set; }

    public async Task OnGetAsync([FromQuery] VinculoFiltro filtro, CancellationToken ct)
    {
        Filtro = filtro;
        Resultado = await CargarAsync(() => vinculos.BuscarAsync(filtro, ct));
        if (Resultado is not null)
            Paginador = PaginadorVista.De(Resultado, new Dictionary<string, string?> { ["Texto"] = filtro.Texto, ["Estado"] = filtro.Estado });

        try
        {
            Conflictos = await vinculos.DetectarConflictosAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<IndexModel>>().LogError(ex, "Error al detectar conflictos de vínculos.");
            ErrorConflictos = "No fue posible verificar conflictos en este momento.";
        }
    }

    public async Task<IActionResult> OnPostDesvincularAsync(int id, string? volver, CancellationToken ct)
    {
        if (!await AutorizarAsync(Permisos.EquiposEditar)) return Forbid();
        Notificar(await EjecutarAsync(() => vinculos.DesactivarAsync(id, UsuarioActual, ct)));
        return VolverALista(volver);
    }
}
