using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChecadorWeb.Pages.Empleados;

[Authorize(Policy = Permisos.EmpleadosEditar)]
public class EditarModel(IEmpleadoService empleados) : PaginaBase
{
    [BindProperty]
    public EmpleadoEdicion Datos { get; set; } = new();

    public EmpleadoResumen? Resumen { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var datos = await CargarAsync(() => empleados.ObtenerEdicionAsync(id, ct));
        if (ErrorCarga is not null) return Page();
        if (datos is null) return NotFound();
        Datos = datos;
        Resumen = await CargarAsync(() => empleados.ObtenerResumenAsync(id, ct));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Resumen = await CargarAsync(() => empleados.ObtenerResumenAsync(id, ct));
            return Page();
        }

        var r = await EjecutarAsync(() => empleados.ActualizarAsync(id, Datos, UsuarioActual, ct));
        if (!r.Exitoso)
        {
            ModelState.AddModelError(string.Empty, r.Mensaje);
            Datos.NuevoPin = Datos.ConfirmarNuevoPin = null;
            Resumen = await CargarAsync(() => empleados.ObtenerResumenAsync(id, ct));
            return Page();
        }

        Notificar(r);
        return RedirectToPage("./Index", new { Texto = id.ToString(), Estado = "todos" });
    }
}
