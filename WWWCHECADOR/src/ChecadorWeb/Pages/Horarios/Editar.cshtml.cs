using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ChecadorWeb.Pages.Horarios;

[Authorize(Policy = Permisos.HorariosEditar)]
public class EditarModel(
    IEmpleadoService empleados,
    IVacacionService vacacionesService,
    IOptionsMonitor<HorariosOptions> horarios) : PaginaBase
{
    [BindProperty]
    public HorarioEdicion Datos { get; set; } = new();

    [BindProperty]
    public VacacionAlta NuevaVacacion { get; set; } = new();

    public EmpleadoResumen? Resumen { get; private set; }
    public IReadOnlyList<VacacionFila> Vacaciones { get; private set; } = [];
    public bool PermiteCruceMedianoche => horarios.CurrentValue.PermitirCruceMedianoche;

    // Nullable: con "string" no anulable MVC la trata como [Required] implícito y rechaza todo POST que no la envíe.
    [BindProperty(SupportsGet = true)]
    public string? TabActiva { get; set; } = "horario";

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var datos = await CargarAsync(() => empleados.ObtenerHorarioAsync(id, ct));
        if (ErrorCarga is not null) return Page();
        if (datos is null) return NotFound();
        Datos = datos;

        Resumen = await CargarAsync(() => empleados.ObtenerResumenAsync(id, ct));
        Vacaciones = await CargarAsync(() => vacacionesService.ListarPorTrabajadorAsync(id, ct)) ?? [];
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken ct)
    {
        TabActiva = "horario";
        AgregarErrores(nameof(Datos), HorarioValidator.Validar(Datos.ACaptura(), horarios.CurrentValue));
        if (ModelState.IsValid)
        {
            var r = await EjecutarAsync(() => empleados.ActualizarHorarioAsync(id, Datos, UsuarioActual, ct));
            if (r.Exitoso)
            {
                Notificar(r);
                return RedirectToPage("./Index", new { Texto = id.ToString(), Estado = "todos" });
            }
            ModelState.AddModelError(string.Empty, r.Mensaje);
        }

        Resumen = await CargarAsync(() => empleados.ObtenerResumenAsync(id, ct));
        Vacaciones = await CargarAsync(() => vacacionesService.ListarPorTrabajadorAsync(id, ct)) ?? [];
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarVacacionesAsync(int id, CancellationToken ct)
    {
        TabActiva = "vacaciones";
        if (NuevaVacacion.FechaInicio is null)
            ModelState.AddModelError("NuevaVacacion.FechaInicio", "La fecha de inicio es requerida.");

        if (NuevaVacacion.FechaFin is null)
            ModelState.AddModelError("NuevaVacacion.FechaFin", "La fecha de fin es requerida.");
        else if (NuevaVacacion.FechaInicio is not null && NuevaVacacion.FechaFin < NuevaVacacion.FechaInicio)
            ModelState.AddModelError("NuevaVacacion.FechaFin", "La fecha final no puede ser menor a la fecha inicial.");

        if (NuevaVacacion.DiasTomados is null or <= 0)
            ModelState.AddModelError("NuevaVacacion.DiasTomados", "Los días tomados deben ser al menos 1.");

        if (ModelState.IsValid)
        {
            var r = await EjecutarAsync(() => vacacionesService.RegistrarAsync(id, NuevaVacacion, UsuarioActual, ct));
            Notificar(r);
            if (r.Exitoso)
            {
                return RedirectToPage(new { id, TabActiva = "vacaciones" });
            }
            ModelState.AddModelError(string.Empty, r.Mensaje);
        }

        var datos = await CargarAsync(() => empleados.ObtenerHorarioAsync(id, ct));
        if (datos is not null) Datos = datos;
        Resumen = await CargarAsync(() => empleados.ObtenerResumenAsync(id, ct));
        Vacaciones = await CargarAsync(() => vacacionesService.ListarPorTrabajadorAsync(id, ct)) ?? [];
        return Page();
    }

    public async Task<IActionResult> OnPostCancelarVacacionesAsync(int id, int idVacacion, CancellationToken ct)
    {
        var r = await EjecutarAsync(() => vacacionesService.CancelarAsync(idVacacion, id, UsuarioActual, ct));
        Notificar(r);
        return RedirectToPage(new { id, TabActiva = "vacaciones" });
    }
}
