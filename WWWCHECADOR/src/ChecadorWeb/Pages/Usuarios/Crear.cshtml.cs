using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChecadorWeb.Pages.Usuarios;

[Authorize(Policy = Permisos.UsuariosEditar)]
public class CrearModel(IUsuarioService usuarios, IPerfilCatalogo perfiles) : PaginaBase
{
    [BindProperty]
    public UsuarioAlta Datos { get; set; } = new();

    public IReadOnlyList<PerfilItem> Perfiles { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) =>
        Perfiles = await CargarAsync(() => perfiles.ObtenerAsync(ct)) ?? [];

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var r = await EjecutarAsync(() => usuarios.CrearAsync(Datos, UsuarioActual, ct));
            if (r.Exitoso)
            {
                Notificar(r);
                return RedirectToPage("./Index", new { Texto = Datos.IdUsuario.Trim(), Estado = "todos" });
            }
            ModelState.AddModelError(string.Empty, r.Mensaje);
        }

        Datos.Password = Datos.ConfirmarPassword = string.Empty;
        Perfiles = await CargarAsync(() => perfiles.ObtenerAsync(ct)) ?? [];
        return Page();
    }
}
