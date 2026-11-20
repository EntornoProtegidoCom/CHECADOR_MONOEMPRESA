using ChecadorWeb.Data;
using ChecadorWeb.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChecadorWeb.Services;

/// <summary>Una sección del panel: si su consulta falla, se informa el error sin afectar a las demás.</summary>
public sealed record SeccionPanel<T>(T? Datos, string? Error) where T : class
{
    public static SeccionPanel<T> Ok(T datos) => new(datos, null);
    public static SeccionPanel<T> Fallo(string error) => new(null, error);
}

public sealed record IndicadoresEmpleados(int Activos, int Inactivos, int SinHorario);
public sealed record IndicadoresUsuarios(int Vigentes, int Inactivos);
public sealed record IndicadoresEquipos(int VinculosActivos, int Conflictos, int ConflictosCriticos);
public sealed record IndicadoresMarcajes(DateOnly Fecha, IReadOnlyDictionary<short, int> PorTipo, int TrabajadoresConEntrada, int CorreosPendientes)
{
    public int Total => PorTipo.Values.Sum();
}

public sealed class PanelResultado
{
    public DateTime? ActualizadoServidor { get; set; }
    public DateTime ActualizadoLocal { get; set; } = DateTime.Now;
    public SeccionPanel<IndicadoresEmpleados>? Empleados { get; set; }
    public SeccionPanel<IndicadoresUsuarios>? Usuarios { get; set; }
    public SeccionPanel<IndicadoresEquipos>? Equipos { get; set; }
    public SeccionPanel<EstacionesResultado>? Estaciones { get; set; }
    public SeccionPanel<IndicadoresMarcajes>? Marcajes { get; set; }
}

public interface IPanelService
{
    Task<PanelResultado> ObtenerAsync(IReadOnlySet<string> permisos, CancellationToken ct = default);
}

public sealed class PanelService(
    ChecadorDbContext db,
    IVinculoService vinculos,
    IEstacionService estaciones,
    IOptionsMonitor<SeguridadOptions> seguridad,
    ILogger<PanelService> logger) : IPanelService
{
    // Las consultas son secuenciales: el DbContext no admite operaciones concurrentes.
    public async Task<PanelResultado> ObtenerAsync(IReadOnlySet<string> permisos, CancellationToken ct = default)
    {
        var r = new PanelResultado();
        try
        {
            r.ActualizadoServidor = await db.Database.SqlQueryRaw<DateTime>("SELECT GETDATE() AS [Value]").SingleAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudo consultar la hora del servidor de base de datos.");
        }

        if (permisos.Contains(Permisos.EmpleadosVer))
            r.Empleados = await Seccion("empleados", async () =>
            {
                var datos = await db.MM_TRABAJADOREs.AsNoTracking()
                    .GroupBy(_ => 1)
                    .Select(g => new IndicadoresEmpleados(
                        g.Count(t => t.status == true),
                        g.Count(t => t.status != true),
                        g.Count(t => t.status == true && (t.HoraEntrada == null || t.HoraSalida == null))))
                    .SingleOrDefaultAsync(ct);
                return datos ?? new IndicadoresEmpleados(0, 0, 0);
            });

        if (permisos.Contains(Permisos.UsuariosVer))
            r.Usuarios = await Seccion("usuarios", async () =>
            {
                var vigente = seguridad.CurrentValue.ValorVigente;
                var vig = await db.SS_USUARIOs.AsNoTracking().CountAsync(u => u.ST_VIGENTE == vigente, ct);
                var total = await db.SS_USUARIOs.AsNoTracking().CountAsync(ct);
                return new IndicadoresUsuarios(vig, total - vig);
            });

        if (permisos.Contains(Permisos.EquiposVer))
            r.Equipos = await Seccion("equipos", async () =>
            {
                var activos = await db.DD_EQUIPO_VINCULOs.AsNoTracking().CountAsync(v => v.Activo, ct);
                var conflictos = await vinculos.DetectarConflictosAsync(ct);
                return new IndicadoresEquipos(activos, conflictos.Count, conflictos.Count(c => c.Critico));
            });

        if (permisos.Contains(Permisos.EstacionesVer))
            r.Estaciones = await Seccion("estaciones", () => estaciones.ObtenerAsync(null, null, ct));

        if (permisos.Contains(Permisos.MarcajesVer))
            r.Marcajes = await Seccion("marcajes", async () =>
            {
                var hoy = (r.ActualizadoServidor ?? DateTime.Now).Date;
                var manana = hoy.AddDays(1);
                var porTipo = await db.MM_HORARIOs.AsNoTracking()
                    .Where(h => h.Horaregistro >= hoy && h.Horaregistro < manana)
                    .GroupBy(h => h.TipoRegistro)
                    .Select(g => new { g.Key, Total = g.Count() })
                    .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
                var conEntrada = await db.MM_HORARIOs.AsNoTracking()
                    .Where(h => h.Horaregistro >= hoy && h.Horaregistro < manana && h.TipoRegistro == 1)
                    .Select(h => h.IDTrabajador).Distinct().CountAsync(ct);
                // Mismo criterio que sp_ConsultaEnviosPendientes.
                var pendientes = await db.MM_HORARIOs.AsNoTracking()
                    .CountAsync(h => h.stEnviado == false && (h.NuIntentos ?? 0) < 2, ct);
                return new IndicadoresMarcajes(DateOnly.FromDateTime(hoy), porTipo, conEntrada, pendientes);
            });

        return r;
    }

    private async Task<SeccionPanel<T>> Seccion<T>(string nombre, Func<Task<T>> consulta) where T : class
    {
        try
        {
            return SeccionPanel<T>.Ok(await consulta());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error al consultar la sección {Seccion} del panel.", nombre);
            return SeccionPanel<T>.Fallo("No fue posible consultar esta información. Intente de nuevo.");
        }
    }
}
