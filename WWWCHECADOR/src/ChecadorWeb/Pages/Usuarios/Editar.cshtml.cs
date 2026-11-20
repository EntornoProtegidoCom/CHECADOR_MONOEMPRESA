using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChecadorWeb.Pages.Usuarios;

[Authorize(Policy = Permisos.UsuariosEditar)]
public class EditarModel(IUsuarioService usuarios, IPerfilCatalogo perfiles) : PaginaBase
{
    [BindProperty]
    public UsuarioEdicion Datos { get; set; } = new();

    public string Id { get; private set; } = string.Empty;
    public IReadOnlyList<PerfilItem> Perfiles { get; private set; } = [];
    public bool EsUsuarioActual => string.Equals(Id, UsuarioActual, StringComparison.OrdinalIgnoreCase);

    public async Task<IActionResult> OnGetAsync(string id, CancellationToken ct)
    {
        Id = id;
        var u = await CargarAsync(() => usuarios.ObtenerAsync(id, ct));
        if (ErrorCarga is not null) return Page();
        if (u is null) return NotFound();
        Datos = new UsuarioEdicion { IdPerfil = u.Perfil, Nombre = u.Nombre ?? string.Empty, Paterno = u.Paterno, Materno = u.Materno, Email = u.Email };
        Perfiles = await CargarAsync(() => perfiles.ObtenerAsync(ct)) ?? [];
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id, CancellationToken ct)
    {
        Id = id;
        if (ModelState.IsValid)
        {
            var r = await EjecutarAsync(() => usuarios.ActualizarAsync(id, Datos, UsuarioActual, ct));
            if (r.Exitoso)
            {
                Notificar(r);
                return RedirectToPage("./Index", new { Texto = id, Estado = "todos" });
            }
            ModelState.AddModelError(string.Empty, r.Mensaje);
        }

        Perfiles = await CargarAsync(() => perfiles.ObtenerAsync(ct)) ?? [];
        return Page();
    }
}
