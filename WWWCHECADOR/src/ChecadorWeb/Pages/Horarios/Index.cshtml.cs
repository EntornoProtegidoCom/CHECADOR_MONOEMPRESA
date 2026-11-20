using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChecadorWeb.Pages.Horarios;

[Authorize(Policy = Permisos.HorariosVer)]
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
            Paginador = PaginadorVista.De(Resultado, new Dictionary<string, string?>
            {
                ["Texto"] = filtro.Texto,
                ["Estado"] = filtro.Estado,
                ["SinHorario"] = filtro.SinHorario ? "true" : null,
            });
    }
}
