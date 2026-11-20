using ChecadorWeb.Data;
using ChecadorWeb.Data.Entities;
using ChecadorWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ChecadorWeb.Services;

public interface IVinculoService
{
    Task<PaginaResultado<VinculoFila>> BuscarAsync(VinculoFiltro filtro, CancellationToken ct = default);
    Task<IReadOnlyList<ConflictoVinculo>> DetectarConflictosAsync(CancellationToken ct = default);
    Task<ResultadoOperacion> CrearAsync(VinculoAlta alta, string usuario, CancellationToken ct = default);
    Task<ResultadoOperacion> DesactivarAsync(int id, string usuario, CancellationToken ct = default);
}

/// <summary>
/// Replica las reglas de sp_desktop_vinculo_equipo (un solo vínculo activo por NBEquipo; desvincular = Activo 0)
/// y agrega validaciones de consistencia del sitio.
/// </summary>
public sealed class VinculoService(ChecadorDbContext db, IAuditoria auditoria) : IVinculoService
{
    public const string CodigoConfirmarMultiple = "CONFIRMAR_MULTIPLE";

    public async Task<PaginaResultado<VinculoFila>> BuscarAsync(VinculoFiltro filtro, CancellationToken ct = default)
    {
        var pagina = Paginacion.NormalizarPagina(filtro.Pagina);
        var tamano = Paginacion.NormalizarTamano(filtro.Tamano);

        var query = db.DD_EQUIPO_VINCULOs.AsNoTracking();
        query = filtro.Estado switch
        {
            "activos" => query.Where(v => v.Activo),
            "inactivos" => query.Where(v => !v.Activo),
            _ => query,
        };

        foreach (var t in EmpleadoService.Terminos(filtro.Texto))
        {
            var esId = int.TryParse(t, out var id);
            query = query.Where(v => v.NBEquipo.Contains(t)
                || (esId && v.IDTrabajador == id)
                || v.IDTrabajadorNavigation.Nombre.Contains(t)
                || v.IDTrabajadorNavigation.ApellidoP.Contains(t)
                || v.IDTrabajadorNavigation.ApellidoM.Contains(t));
        }

        var total = await query.CountAsync(ct);
        var elementos = await query
            .OrderByDescending(v => v.Activo).ThenBy(v => v.NBEquipo).ThenByDescending(v => v.ID)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(v => new VinculoFila(
                v.ID, v.NBEquipo, v.IDTrabajador,
                v.IDTrabajadorNavigation.ApellidoP + " " + v.IDTrabajadorNavigation.ApellidoM + " " + v.IDTrabajadorNavigation.Nombre,
                v.IDTrabajadorNavigation.status,
                v.Activo, v.create_time, v.Create_ID_USUARIO, v.update_time, v.Update_ID_USUARIO,
                db.DD_EQUIPO_HEARTBEATs.Where(h => h.NBEquipo == v.NBEquipo).Select(h => (DateTime?)h.UltimoHeartbeat).FirstOrDefault()))
            .ToListAsync(ct);

        return new PaginaResultado<VinculoFila> { Elementos = elementos, Pagina = pagina, TamanoPagina = tamano, Total = total };
    }

    public async Task<IReadOnlyList<ConflictoVinculo>> DetectarConflictosAsync(CancellationToken ct = default)
    {
        var conflictos = new List<ConflictoVinculo>();

        var activos = await db.DD_EQUIPO_VINCULOs.AsNoTracking()
            .Where(v => v.Activo)
            .Select(v => new { v.NBEquipo, v.IDTrabajador, TrabajadorActivo = v.IDTrabajadorNavigation.status })
            .ToListAsync(ct);

        foreach (var g in activos.GroupBy(v => v.NBEquipo, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            conflictos.Add(new(TipoConflicto.EquipoConVariosVinculosActivos, g.Key,
                $"El equipo tiene {g.Count()} vínculos activos (trabajadores {string.Join(", ", g.Select(x => x.IDTrabajador))}). El checador solo usa uno.", true));

        foreach (var g in activos.GroupBy(v => v.IDTrabajador).Where(g => g.Count() > 1))
            conflictos.Add(new(TipoConflicto.TrabajadorConVariosEquipos, string.Join(", ", g.Select(x => x.NBEquipo)),
                $"El trabajador {g.Key} está vinculado a {g.Count()} equipos.", false));

        foreach (var v in activos.Where(v => v.TrabajadorActivo != true))
            conflictos.Add(new(TipoConflicto.VinculoConTrabajadorInactivo, v.NBEquipo,
                $"El trabajador {v.IDTrabajador} está inactivo; el checador rechazará sus marcajes en este equipo.", true));

        var heartbeats = await db.DD_EQUIPO_HEARTBEATs.AsNoTracking()
            .Select(h => new { h.NBEquipo, h.IDTrabajador })
            .ToListAsync(ct);
        var porEquipo = activos.ToLookup(v => v.NBEquipo, StringComparer.OrdinalIgnoreCase);

        foreach (var h in heartbeats)
        {
            var vinculos = porEquipo[h.NBEquipo].ToList();
            if (vinculos.Count == 0)
                conflictos.Add(new(TipoConflicto.HeartbeatSinVinculo, h.NBEquipo,
                    $"La estación reporta heartbeat (trabajador {h.IDTrabajador}) pero no tiene vínculo activo.", false));
            else if (vinculos.All(v => v.IDTrabajador != h.IDTrabajador))
                conflictos.Add(new(TipoConflicto.HeartbeatTrabajadorDistinto, h.NBEquipo,
                    $"El heartbeat informa al trabajador {h.IDTrabajador}, pero el vínculo activo es de {string.Join(", ", vinculos.Select(v => v.IDTrabajador))}.", true));
        }

        return conflictos.OrderByDescending(c => c.Critico).ThenBy(c => c.NbEquipo).ToList();
    }

    public async Task<ResultadoOperacion> CrearAsync(VinculoAlta alta, string usuario, CancellationToken ct = default)
    {
        var equipo = alta.NbEquipo.Trim();
        var idTrabajador = alta.IdTrabajador!.Value;

        await using var tx = await db.Database.IniciarTransaccionAsync(System.Data.IsolationLevel.ReadCommitted, ct);

        // Bloquea el rango del equipo para que dos altas simultáneas no creen dos vínculos activos.
        var activosEquipo = await db.DD_EQUIPO_VINCULOs
            .FromSqlInterpolated($"SELECT * FROM dbo.DD_EQUIPO_VINCULO WITH (UPDLOCK, HOLDLOCK) WHERE NBEquipo = {equipo} AND Activo = 1")
            .AsNoTracking()
            .Select(v => v.IDTrabajador)
            .ToListAsync(ct);

        if (activosEquipo.Count > 0)
            return ResultadoOperacion.Advertencia(
                $"El equipo {equipo} ya está vinculado al trabajador {string.Join(", ", activosEquipo)}. Desvincúlelo antes de crear un nuevo vínculo.");

        var trabajador = await db.MM_TRABAJADOREs.AsNoTracking()
            .Where(t => t.IDTrabajador == idTrabajador)
            .Select(t => new { t.status })
            .SingleOrDefaultAsync(ct);
        if (trabajador is null) return ResultadoOperacion.Fallo("El empleado no existe.");
        if (trabajador.status != true)
            return ResultadoOperacion.Advertencia("El empleado está inactivo; sus marcajes por vínculo serían rechazados. Actívelo primero.");

        var otrosEquipos = await db.DD_EQUIPO_VINCULOs.AsNoTracking()
            .Where(v => v.Activo && v.IDTrabajador == idTrabajador)
            .Select(v => v.NBEquipo).ToListAsync(ct);
        if (otrosEquipos.Count > 0 && !alta.ConfirmarMultiple)
            return new ResultadoOperacion(TipoResultado.Advertencia,
                $"El empleado ya está vinculado a: {string.Join(", ", otrosEquipos)}. Marque la confirmación si desea vincularlo también a {equipo}.",
                CodigoConfirmarMultiple);

        var ahora = DateTime.Now;
        db.DD_EQUIPO_VINCULOs.Add(new DD_EQUIPO_VINCULO
        {
            NBEquipo = equipo,
            IDTrabajador = idTrabajador,
            Activo = true,
            create_time = ahora,
            Create_ID_USUARIO = usuario,
            update_time = ahora,
            Update_ID_USUARIO = usuario,
        });
        await db.SaveChangesAsync(ct);
        await tx.ConfirmarAsync(ct);
        db.ChangeTracker.Clear();

        await auditoria.RegistrarAsync(new EventoAuditoria("VINCULAR", "DD_EQUIPO_VINCULO", equipo, true, $"IDTrabajador={idTrabajador}"), ct);
        return ResultadoOperacion.Ok($"Equipo {equipo} vinculado al empleado {idTrabajador}.");
    }

    public async Task<ResultadoOperacion> DesactivarAsync(int id, string usuario, CancellationToken ct = default)
    {
        var vinculo = await db.DD_EQUIPO_VINCULOs.AsNoTracking()
            .Where(v => v.ID == id).Select(v => new { v.NBEquipo, v.IDTrabajador, v.Activo }).SingleOrDefaultAsync(ct);
        if (vinculo is null) return ResultadoOperacion.Fallo("El vínculo no existe.");
        if (!vinculo.Activo) return ResultadoOperacion.Advertencia("El vínculo ya estaba inactivo.");

        var ahora = DateTime.Now;
        var filas = await db.DD_EQUIPO_VINCULOs
            .Where(v => v.ID == id && v.Activo)
            .ExecuteUpdateAsync(u => u
                .SetProperty(v => v.Activo, false)
                .SetProperty(v => v.update_time, ahora)
                .SetProperty(v => v.Update_ID_USUARIO, usuario), ct);
        if (filas == 0) return ResultadoOperacion.Advertencia("El vínculo cambió mientras se procesaba la solicitud.");

        await auditoria.RegistrarAsync(new EventoAuditoria("DESVINCULAR", "DD_EQUIPO_VINCULO", vinculo.NBEquipo, true,
            $"ID={id}; IDTrabajador={vinculo.IDTrabajador}"), ct);
        return ResultadoOperacion.Ok($"Equipo {vinculo.NBEquipo} desvinculado.");
    }
}
