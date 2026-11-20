using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChecadorWeb.Pages.Equipos;

[Authorize(Policy = Permisos.EquiposEditar)]
public class VincularModel(IVinculoService vinculos, IEmpleadoService empleados) : PaginaBase
{
    [BindProperty]
    public VinculoAlta Datos { get; set; } = new();

    public IReadOnlyList<EmpleadoOpcion>? Empleados { get; private set; }
    public bool RequiereConfirmacion { get; private set; }

    public async Task OnGetAsync(string? equipo, int? trabajador, CancellationToken ct)
    {
        Datos.NbEquipo = equipo?.Trim() ?? string.Empty;
        Datos.IdTrabajador = trabajador;
        Empleados = await CargarAsync(() => empleados.ListarOpcionesAsync(soloActivos: true, ct));
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await EjecutarAsync(() => vinculos.CrearAsync(Datos, UsuarioActual, ct));
            if (r.Exitoso)
            {
                Notificar(r);
                return RedirectToPage("./Index", new { Texto = Datos.NbEquipo.Trim(), Estado = "todos" });
            }
            RequiereConfirmacion = r.Codigo == VinculoService.CodigoConfirmarMultiple;
            ModelState.AddModelError(string.Empty, r.Mensaje);
        }

        Empleados = await CargarAsync(() => empleados.ListarOpcionesAsync(soloActivos: true, ct));
        return Page();
    }
}
