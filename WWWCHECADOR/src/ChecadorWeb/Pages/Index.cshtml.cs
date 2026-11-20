using ChecadorWeb.Infrastructure;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChecadorWeb.Pages;

public class IndexModel(IPanelService panel, IPermisoResolver resolver) : PaginaBase
{
    public PanelResultado Resultado { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!User.TryGetPerfil(out var perfil)) return Forbid();
        var permisos = resolver.ObtenerPermisos(perfil);

        if (!permisos.Contains(Permisos.PanelVer))
        {
            // Envía al primer módulo permitido en lugar de mostrar un error.
            var destino = new (string Permiso, string Pagina)[]
            {
                (Permisos.EmpleadosVer, "/Empleados/Index"), (Permisos.HorariosVer, "/Horarios/Index"),
                (Permisos.EquiposVer, "/Equipos/Index"), (Permisos.EstacionesVer, "/Estaciones/Index"),
                (Permisos.MarcajesVer, "/Marcajes/Index"), (Permisos.UsuariosVer, "/Usuarios/Index"),
            }.FirstOrDefault(d => permisos.Contains(d.Permiso));
            return destino.Pagina is null ? RedirectToPage("/Account/MiCuenta") : RedirectToPage(destino.Pagina);
        }

        Resultado = await panel.ObtenerAsync(permisos, ct);
        return Page();
    }
}
