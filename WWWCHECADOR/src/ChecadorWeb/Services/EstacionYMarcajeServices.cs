using ChecadorWeb.Data;
using ChecadorWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChecadorWeb.Services;

public sealed record EstacionFila(
    string NbEquipo,
    int? IdTrabajadorReportado,
    string? NombreTrabajador,
    DateTime? UltimoHeartbeat,
    int? MinutosSinHeartbeat,
    string? Ip,
    string? VersionApp,
    string? EstadoInformado,
    EstadoComunicacion Estado,
    int? IdTrabajadorVinculado,
    string? Observacion);

public sealed record EstacionesResultado(
    DateTime AhoraServidor,
    int MinutosAviso,
    int MinutosDesconexion,
    IReadOnlyList<EstacionFila> Filas)
{
    public int Contar(EstadoComunicacion e) => Filas.Count(f => f.Estado == e);
}

public interface IEstacionService
{
    Task<EstacionesResultado> ObtenerAsync(int? minutosAviso, int? minutosDesconexion, CancellationToken ct = default);
}

public sealed class EstacionService(ChecadorDbContext db, IOptionsMonitor<HeartbeatOptions> opciones) : IEstacionService
{
    public async Task<EstacionesResultado> ObtenerAsync(int? minutosAviso, int? minutosDesconexion, CancellationToken ct = default)
    {
        var cfg = opciones.CurrentValue;
        var (aviso, desconexion) = HeartbeatClasificador.NormalizarUmbrales(minutosAviso, minutosDesconexion, cfg);

        var ahora = await db.Database.SqlQueryRaw<DateTime>("SELECT GETDATE() AS [Value]").SingleAsync(ct);

        var heartbeats = await db.DD_EQUIPO_HEARTBEATs.AsNoTracking()
            .Select(h => new
            {
                h.NBEquipo, h.IDTrabajador, h.UltimoHeartbeat, h.IPRegistro, h.VersionApp, h.EstadoEstacion,
                Nombre = db.MM_TRABAJADOREs.Where(t => t.IDTrabajador == h.IDTrabajador)
                    .Select(t => t.ApellidoP + " " + t.ApellidoM + " " + t.Nombre).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var vinculos = await db.DD_EQUIPO_VINCULOs.AsNoTracking()
            .Where(v => v.Activo)
            .Select(v => new
            {
                v.NBEquipo, v.IDTrabajador,
                Nombre = v.IDTrabajadorNavigation.ApellidoP + " " + v.IDTrabajadorNavigation.ApellidoM + " " + v.IDTrabajadorNavigation.Nombre,
            })
            .ToListAsync(ct);
        var vinculoPorEquipo = vinculos.GroupBy(v => v.NBEquipo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var filas = new List<EstacionFila>();
        foreach (var h in heartbeats)
        {
            vinculoPorEquipo.TryGetValue(h.NBEquipo, out var v);
            var estado = HeartbeatClasificador.Clasificar(h.UltimoHeartbeat, ahora, aviso, desconexion, cfg.MinutosToleranciaFuturo);
            string? obs = v is null
                ? "Sin vínculo activo"
                : v.IDTrabajador != h.IDTrabajador ? $"Vínculo activo con trabajador {v.IDTrabajador}" : null;
            filas.Add(new EstacionFila(
                h.NBEquipo, h.IDTrabajador, h.Nombre, h.UltimoHeartbeat,
                (int)Math.Floor((ahora - h.UltimoHeartbeat).TotalMinutes),
                h.IPRegistro, h.VersionApp, h.EstadoEstacion, estado, v?.IDTrabajador, obs));
        }

        var conHeartbeat = new HashSet<string>(heartbeats.Select(h => h.NBEquipo), StringComparer.OrdinalIgnoreCase);
        foreach (var v in vinculoPorEquipo.Values.Where(v => !conHeartbeat.Contains(v.NBEquipo)))
        {
            filas.Add(new EstacionFila(v.NBEquipo, null, v.Nombre, null, null, null, null, null,
                EstadoComunicacion.NuncaReporto, v.IDTrabajador, "Vinculada, sin registro de heartbeat"));
        }

        var orden = filas
            .OrderBy(f => f.Estado switch
            {
                EstadoComunicacion.SinComunicacion => 0,
                EstadoComunicacion.Aviso => 1,
                EstadoComunicacion.HoraInconsistente => 2,
                EstadoComunicacion.NuncaReporto => 3,
                _ => 4,
            })
            .ThenByDescending(f => f.MinutosSinHeartbeat)
            .ThenBy(f => f.NbEquipo)
            .ToList();

        return new EstacionesResultado(ahora, aviso, desconexion, orden);
    }
}

public sealed class MarcajeFiltro
{
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public string? Trabajador { get; set; }
    public short? Tipo { get; set; }
    public string? Equipo { get; set; }
    public int? Pagina { get; set; }
    public int? Tamano { get; set; }
}

public sealed record MarcajeFila(
    int Id, DateTime Hora, short Tipo, int? IdTrabajador, string? NombreTrabajador,
    string? Equipo, string? Ip, bool TieneFoto, bool? Enviado, short? Intentos)
{
    public string TipoNombre => TiposRegistro.Nombre(Tipo);
}

public interface IMarcajeService
{
    Task<PaginaResultado<MarcajeFila>> BuscarAsync(MarcajeFiltro filtro, CancellationToken ct = default);
    Task<List<MarcajeFila>> ExportarAsync(MarcajeFiltro filtro, CancellationToken ct = default);
    Task<byte[]?> ObtenerFotoAsync(int id, CancellationToken ct = default);
}

/// <summary>Consulta de solo lectura de MM_HORARIO (marcajes). No modifica asistencia.</summary>
public sealed class MarcajeService(ChecadorDbContext db) : IMarcajeService
{
    public const int DiasMaximosConsulta = 366;

    public async Task<PaginaResultado<MarcajeFila>> BuscarAsync(MarcajeFiltro filtro, CancellationToken ct = default)
    {
        var pagina = Paginacion.NormalizarPagina(filtro.Pagina);
        var tamano = Paginacion.NormalizarTamano(filtro.Tamano);
        var query = Consultar(filtro);
        var total = await query.CountAsync(ct);
        var elementos = await query.Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(ct);

        return new PaginaResultado<MarcajeFila> { Elementos = elementos, Pagina = pagina, TamanoPagina = tamano, Total = total };
    }

    public Task<List<MarcajeFila>> ExportarAsync(MarcajeFiltro filtro, CancellationToken ct = default) =>
        Consultar(filtro).ToListAsync(ct);

    private IQueryable<MarcajeFila> Consultar(MarcajeFiltro filtro)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        filtro.Hasta ??= hoy;
        filtro.Desde ??= filtro.Hasta.Value.AddDays(-6);
        if (filtro.Desde > filtro.Hasta) (filtro.Desde, filtro.Hasta) = (filtro.Hasta, filtro.Desde);
        if (filtro.Hasta.Value.DayNumber - filtro.Desde.Value.DayNumber > DiasMaximosConsulta)
            filtro.Desde = filtro.Hasta.Value.AddDays(-DiasMaximosConsulta);

        var desde = filtro.Desde.Value.ToDateTime(TimeOnly.MinValue);
        var hasta = filtro.Hasta.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var query = db.MM_HORARIOs.AsNoTracking().Where(h => h.Horaregistro >= desde && h.Horaregistro < hasta);
        if (filtro.Tipo is { } tipo) query = query.Where(h => h.TipoRegistro == tipo);
        if (!string.IsNullOrWhiteSpace(filtro.Equipo))
        {
            var equipo = filtro.Equipo.Trim();
            query = query.Where(h => h.NBEquipo != null && h.NBEquipo.Contains(equipo));
        }
        foreach (var t in EmpleadoService.Terminos(filtro.Trabajador))
        {
            if (int.TryParse(t, out var id))
                query = query.Where(h => h.IDTrabajador == id);
            else
                query = query.Where(h => h.IDTrabajadorNavigation != null
                    && (h.IDTrabajadorNavigation.Nombre.Contains(t) || h.IDTrabajadorNavigation.ApellidoP.Contains(t) || h.IDTrabajadorNavigation.ApellidoM.Contains(t)));
        }

        return query
            .OrderByDescending(h => h.Horaregistro).ThenByDescending(h => h.ID)
            .Select(h => new MarcajeFila(
                h.ID, h.Horaregistro, h.TipoRegistro, h.IDTrabajador,
                h.IDTrabajadorNavigation != null
                    ? h.IDTrabajadorNavigation.ApellidoP + " " + h.IDTrabajadorNavigation.ApellidoM + " " + h.IDTrabajadorNavigation.Nombre
                    : null,
                h.NBEquipo, h.IPRegistro, h.Foto != null, h.stEnviado, h.NuIntentos));
    }

    public Task<byte[]?> ObtenerFotoAsync(int id, CancellationToken ct = default) =>
        db.MM_HORARIOs.AsNoTracking().Where(h => h.ID == id).Select(h => h.Foto).SingleOrDefaultAsync(ct);
}
