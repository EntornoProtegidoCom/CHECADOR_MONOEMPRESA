using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChecadorWeb.Pages.Empleados;

[Authorize(Policy = Permisos.EmpleadosVer)]
public class IndexModel(IEmpleadoService empleados) : PaginaBase
{
    public EmpleadoFiltro Filtro { get; private set; } = new();
    public PaginaResultado<EmpleadoFila>? Resultado { get; private set; }
    public PaginadorVista? Paginador { get; private set; }

    public async Task OnGetAsync([FromQuery] EmpleadoFiltro filtro, CancellationToken ct)
    {
        Filtro = filtro;
        Resultado = await CargarAsync(() => empleados.BuscarAsync(filtro, ct));
        if (Resultado is not null)
            Paginador = PaginadorVista.De(Resultado, new Dictionary<string, string?> { ["Texto"] = filtro.Texto, ["Estado"] = filtro.Estado });
    }

    public async Task<IActionResult> OnPostEstadoAsync(int id, bool activar, string? volver, CancellationToken ct)
    {
        if (!await AutorizarAsync(Permisos.EmpleadosEditar)) return Forbid();
        Notificar(await EjecutarAsync(() => empleados.CambiarEstadoAsync(id, activar, UsuarioActual, ct)));
        return VolverALista(volver);
    }
}
