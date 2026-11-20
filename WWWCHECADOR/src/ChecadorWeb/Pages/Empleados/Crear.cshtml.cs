using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ChecadorWeb.Pages.Empleados;

[Authorize(Policy = Permisos.EmpleadosEditar)]
public class CrearModel(IEmpleadoService empleados, IOptionsMonitor<HorariosOptions> horarios) : PaginaBase
{
    [BindProperty]
    public EmpleadoAlta Datos { get; set; } = new();

    public bool PermiteCruceMedianoche => horarios.CurrentValue.PermitirCruceMedianoche;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!Datos.Horario.EstaVacio)
            AgregarErrores($"{nameof(Datos)}.{nameof(Datos.Horario)}", HorarioValidator.Validar(Datos.Horario.ACaptura(), horarios.CurrentValue));
        if (!ModelState.IsValid) return Page();

        (ResultadoOperacion Resultado, int? Id) r;
        try
        {
            r = await empleados.CrearAsync(Datos, UsuarioActual, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<CrearModel>>().LogError(ex, "Error al crear empleado.");
            r = (ResultadoOperacion.Fallo("No fue posible crear el empleado. Intente de nuevo."), null);
        }

        if (!r.Resultado.Exitoso)
        {
            var clave = r.Resultado.Codigo == EmpleadoService.CodigoIdTrabajadorDuplicado
                ? $"{nameof(Datos)}.{nameof(Datos.IDTrabajador)}"
                : string.Empty;
            ModelState.AddModelError(clave, r.Resultado.Mensaje);
            return Page();
        }

        Notificar(r.Resultado);
        return RedirectToPage("./Index", new { Texto = r.Id.ToString(), Estado = "todos" });
    }
}
