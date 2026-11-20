using ChecadorWeb.Infrastructure;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ChecadorWeb.Pages.Estaciones;

[Authorize(Policy = Permisos.EstacionesVer)]
public class IndexModel(IEstacionService estaciones, IOptionsMonitor<HeartbeatOptions> opciones, ILogger<IndexModel> logger) : PaginaBase
{
    public EstacionesResultado? Resultado { get; private set; }
    public int MinutosAviso { get; private set; }
    public int MinutosDesconexion { get; private set; }
    public int SegundosActualizacion => Math.Max(10, opciones.CurrentValue.SegundosActualizacion);
    public HeartbeatOptions Predeterminados => opciones.CurrentValue;

    public async Task OnGetAsync(int? aviso, int? desconexion, CancellationToken ct)
    {
        (MinutosAviso, MinutosDesconexion) = HeartbeatClasificador.NormalizarUmbrales(aviso, desconexion, opciones.CurrentValue);
        Resultado = await CargarAsync(() => estaciones.ObtenerAsync(MinutosAviso, MinutosDesconexion, ct));
    }

    public async Task<IActionResult> OnGetDatosAsync(int? aviso, int? desconexion, CancellationToken ct)
    {
        try
        {
            var r = await estaciones.ObtenerAsync(aviso, desconexion, ct);
            return new JsonResult(new
            {
                ahora = r.AhoraServidor.ToString("dd/MM/yyyy HH:mm:ss"),
                aviso = r.MinutosAviso,
                desconexion = r.MinutosDesconexion,
                resumen = Enum.GetValues<EstadoComunicacion>().ToDictionary(e => e.ToString(), r.Contar),
                filas = r.Filas.Select(f => new
                {
                    equipo = f.NbEquipo,
                    trabajador = f.IdTrabajadorReportado ?? f.IdTrabajadorVinculado,
                    nombre = f.NombreTrabajador,
                    ultimo = f.UltimoHeartbeat?.ToString("dd/MM/yyyy HH:mm:ss"),
                    minutos = f.MinutosSinHeartbeat,
                    ip = f.Ip,
                    version = f.VersionApp,
                    informado = f.EstadoInformado,
                    estado = f.Estado.ToString(),
                    etiqueta = HeartbeatClasificador.Etiqueta(f.Estado),
                    clase = HeartbeatClasificador.ClaseCss(f.Estado),
                    observacion = f.Observacion,
                }),
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error al actualizar el estado de estaciones.");
            return new JsonResult(new { error = "No fue posible consultar el estado de las estaciones." })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
            };
        }
    }
}
