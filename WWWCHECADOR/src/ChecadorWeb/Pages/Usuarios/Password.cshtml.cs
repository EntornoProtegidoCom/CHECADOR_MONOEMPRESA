using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChecadorWeb.Pages.Usuarios;

[Authorize(Policy = Permisos.UsuariosEditar)]
public class PasswordModel(IUsuarioService usuarios) : PaginaBase
{
    [BindProperty]
    public RestablecerPassword Datos { get; set; } = new();

    public UsuarioFila? Usuario { get; private set; }

    public async Task<IActionResult> OnGetAsync(string id, CancellationToken ct)
    {
        if (string.Equals(id, UsuarioActual, StringComparison.OrdinalIgnoreCase)) return RedirectToPage("/Account/MiCuenta");
        Usuario = await CargarAsync(() => usuarios.ObtenerAsync(id, ct));
        if (ErrorCarga is null && Usuario is null) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id, CancellationToken ct)
    {
        // El cambio propio exige la contraseña actual (Mi cuenta).
        if (string.Equals(id, UsuarioActual, StringComparison.OrdinalIgnoreCase)) return RedirectToPage("/Account/MiCuenta");

        if (ModelState.IsValid)
        {
            var r = await EjecutarAsync(() => usuarios.RestablecerPasswordAsync(id, Datos.Nueva, UsuarioActual, ct));
            if (r.Exitoso)
            {
                Notificar(r);
                return RedirectToPage("./Index", new { Texto = id, Estado = "todos" });
            }
            ModelState.AddModelError(string.Empty, r.Mensaje);
        }

        Datos = new RestablecerPassword();
        Usuario = await CargarAsync(() => usuarios.ObtenerAsync(id, ct));
        return Page();
    }
}
