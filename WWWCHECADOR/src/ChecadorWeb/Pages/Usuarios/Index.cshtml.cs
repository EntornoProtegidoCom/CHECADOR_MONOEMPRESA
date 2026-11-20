using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ChecadorWeb.Pages.Usuarios;

[Authorize(Policy = Permisos.UsuariosVer)]
public class IndexModel(IUsuarioService usuarios, IPerfilCatalogo perfiles, IOptionsMonitor<SeguridadOptions> seguridad) : PaginaBase
{
    public UsuarioFiltro Filtro { get; private set; } = new();
    public PaginaResultado<UsuarioFila>? Resultado { get; private set; }
    public PaginadorVista? Paginador { get; private set; }
    public IReadOnlyList<PerfilItem> Perfiles { get; private set; } = [];
    public string ValorVigente => seguridad.CurrentValue.ValorVigente;

    public async Task OnGetAsync([FromQuery] UsuarioFiltro filtro, CancellationToken ct)
    {
        Filtro = filtro;
        Perfiles = await CargarAsync(() => perfiles.ObtenerAsync(ct)) ?? [];
        Resultado = await CargarAsync(() => usuarios.BuscarAsync(filtro, ct));
        if (Resultado is not null)
            Paginador = PaginadorVista.De(Resultado, new Dictionary<string, string?>
            {
                ["Texto"] = filtro.Texto,
                ["Estado"] = filtro.Estado,
                ["Perfil"] = filtro.Perfil?.ToString(),
            });
    }

    public async Task<IActionResult> OnPostEstadoAsync(string id, bool activar, string? volver, CancellationToken ct)
    {
        if (!await AutorizarAsync(Permisos.UsuariosEditar)) return Forbid();
        Notificar(await EjecutarAsync(() => usuarios.CambiarEstadoAsync(id, activar, UsuarioActual, ct)));
        return VolverALista(volver);
    }

    public string NombrePerfil(int id) => Perfiles.FirstOrDefault(p => p.Id == id)?.Nombre ?? $"Perfil {id} (sin catálogo)";
}
