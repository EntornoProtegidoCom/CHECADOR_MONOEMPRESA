using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChecadorWeb.Pages.DiasFestivos;

[Authorize(Policy = Permisos.DiasFestivosVer)]
public class IndexModel(IDiaFestivoService diaFestivoService) : PaginaBase
{
    // ── Datos de la vista ─────────────────────────────────────────────────────

    public IReadOnlyList<DiaFestivoFila> Festivos { get; private set; } = [];

    [BindProperty]
    public DiaFestivoCaptura Captura { get; set; } = new();

    // ── Parámetro de filtro: mostrar inactivos también ────────────────────────
    [BindProperty(SupportsGet = true)]
    public bool MostrarTodos { get; set; }

    // ── GET ───────────────────────────────────────────────────────────────────

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Si hay un ID de edición en la query, pre-carga el formulario
        if (Request.Query.TryGetValue("editar", out var editarStr) &&
            int.TryParse(editarStr, out var editarId) && editarId > 0)
        {
            var edicion = await diaFestivoService.ObtenerEdicionAsync(editarId, ct);
            if (edicion is not null)
                Captura = edicion;
        }

        await CargarListaAsync(ct);
    }

    // ── POST: Guardar (Alta o Edición) ────────────────────────────────────────

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
    {
        if (!await AutorizarAsync(Permisos.DiasFestivosEditar)) return Forbid();

        if (!ModelState.IsValid)
        {
            await CargarListaAsync(ct);
            return Page();
        }

        Notificar(await EjecutarAsync(() => diaFestivoService.GuardarAsync(Captura, UsuarioActual, ct)));

        return RedirectToPage(MostrarTodos ? new { MostrarTodos = true } : null);
    }

    // ── POST: Toggle estatus (Activar / Desactivar) ───────────────────────────

    public async Task<IActionResult> OnPostToggleAsync(int id, bool activar, CancellationToken ct)
    {
        if (!await AutorizarAsync(Permisos.DiasFestivosEditar)) return Forbid();

        Notificar(await EjecutarAsync(() => diaFestivoService.CambiarEstatusAsync(id, activar, UsuarioActual, ct)));

        return RedirectToPage(MostrarTodos ? new { MostrarTodos = true } : null);
    }

    // ── POST: Eliminar lógico ─────────────────────────────────────────────────

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        if (!await AutorizarAsync(Permisos.DiasFestivosEditar)) return Forbid();

        Notificar(await EjecutarAsync(() => diaFestivoService.EliminarLogicoAsync(id, UsuarioActual, ct)));

        return RedirectToPage(MostrarTodos ? new { MostrarTodos = true } : null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task CargarListaAsync(CancellationToken ct)
    {
        var resultado = await CargarAsync(() =>
            MostrarTodos
                ? diaFestivoService.ListarTodosAsync(ct)
                : diaFestivoService.ListarActivosAsync(ct));

        Festivos = resultado ?? [];
    }
}
