using ChecadorWeb.Data;
using ChecadorWeb.Data.Entities;
using ChecadorWeb.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ChecadorWeb.Services;

public interface IDiaFestivoService
{
    Task<IReadOnlyList<DiaFestivoFila>> ListarActivosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DiaFestivoFila>> ListarTodosAsync(CancellationToken ct = default);
    Task<DiaFestivoCaptura?> ObtenerEdicionAsync(int id, CancellationToken ct = default);
    Task<ResultadoOperacion> GuardarAsync(DiaFestivoCaptura captura, string usuario, CancellationToken ct = default);
    Task<ResultadoOperacion> CambiarEstatusAsync(int id, bool activar, string usuario, CancellationToken ct = default);
    Task<ResultadoOperacion> EliminarLogicoAsync(int id, string usuario, CancellationToken ct = default);

    /// <summary>
    /// Devuelve el conjunto de fechas festivas activas en un rango dado.
    /// Usado por el motor de asistencias para omitir faltas en días festivos.
    /// </summary>
    Task<HashSet<DateOnly>> ObtenerFestivosEnRangoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default);
}

public sealed class DiaFestivoService(ChecadorDbContext db, IAuditoria auditoria) : IDiaFestivoService
{
    // ── Consultas ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DiaFestivoFila>> ListarActivosAsync(CancellationToken ct = default) =>
        await Consulta(db.DiaFestivos.Where(d => d.Estatus)).ToListAsync(ct);

    public async Task<IReadOnlyList<DiaFestivoFila>> ListarTodosAsync(CancellationToken ct = default) =>
        await Consulta(db.DiaFestivos).ToListAsync(ct);

    public Task<DiaFestivoCaptura?> ObtenerEdicionAsync(int id, CancellationToken ct = default) =>
        db.DiaFestivos.AsNoTracking()
            .Where(d => d.ID == id)
            .Select(d => new DiaFestivoCaptura
            {
                Id = d.ID,
                Fecha = DateOnly.FromDateTime(d.Fecha),
                Descripcion = d.Descripcion,
                EsOficial = d.EsOficial,
            })
            .SingleOrDefaultAsync(ct);

    public async Task<HashSet<DateOnly>> ObtenerFestivosEnRangoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        var desdeDate = desde.ToDateTime(TimeOnly.MinValue);
        var hastaDate = hasta.ToDateTime(TimeOnly.MaxValue);

        var fechas = await db.DiaFestivos.AsNoTracking()
            .Where(d => d.Estatus && d.Fecha >= desdeDate && d.Fecha <= hastaDate)
            .Select(d => d.Fecha)
            .ToListAsync(ct);

        return fechas.Select(DateOnly.FromDateTime).ToHashSet();
    }

    // ── Escritura ─────────────────────────────────────────────────────────────

    public async Task<ResultadoOperacion> GuardarAsync(DiaFestivoCaptura captura, string usuario, CancellationToken ct = default)
    {
        if (captura.Fecha is not { } fecha)
            return ResultadoOperacion.Fallo("La fecha es obligatoria.");

        var fechaDate = fecha.ToDateTime(TimeOnly.MinValue);
        var ahora = DateTime.Now;

        // ── Edición ──────────────────────────────────────────────────────────
        if (captura.Id > 0)
        {
            var existeFechaDuplicada = await db.DiaFestivos
                .AnyAsync(d => d.Fecha == fechaDate && d.ID != captura.Id, ct);

            if (existeFechaDuplicada)
                return ResultadoOperacion.Advertencia($"La fecha {fecha:dd/MM/yyyy} ya está registrada en otro día festivo.");

            var filas = await db.DiaFestivos
                .Where(d => d.ID == captura.Id)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(d => d.Fecha, fechaDate)
                    .SetProperty(d => d.Descripcion, captura.Descripcion.Trim())
                    .SetProperty(d => d.EsOficial, captura.EsOficial)
                    .SetProperty(d => d.update_time, ahora)
                    .SetProperty(d => d.Update_ID_USUARIO, usuario), ct);

            if (filas == 0)
                return ResultadoOperacion.Fallo("El día festivo no fue encontrado o ya no existe.");

            await auditoria.RegistrarAsync(new EventoAuditoria("EDICION", "MM_DIAS_FESTIVOS", captura.Id.ToString(), true,
                $"Fecha={fecha:yyyy-MM-dd}; Descripcion={captura.Descripcion}; EsOficial={captura.EsOficial}"), ct);

            return ResultadoOperacion.Ok($"Día festivo «{captura.Descripcion}» actualizado correctamente.");
        }

        // ── Alta ─────────────────────────────────────────────────────────────
        var existe = await db.DiaFestivos.AnyAsync(d => d.Fecha == fechaDate, ct);
        if (existe)
            return ResultadoOperacion.Advertencia($"La fecha {fecha:dd/MM/yyyy} ya está registrada como día festivo.");

        var entidad = new DiaFestivo
        {
            Fecha = fechaDate,
            Descripcion = captura.Descripcion.Trim(),
            EsOficial = captura.EsOficial,
            Estatus = true,
            create_time = ahora,
            Create_ID_USUARIO = usuario,
            update_time = ahora,
            Update_ID_USUARIO = usuario,
        };

        db.DiaFestivos.Add(entidad);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return ResultadoOperacion.Advertencia($"La fecha {fecha:dd/MM/yyyy} ya existe (índice único). No se guardó.");
        }

        await auditoria.RegistrarAsync(new EventoAuditoria("ALTA", "MM_DIAS_FESTIVOS", entidad.ID.ToString(), true,
            $"Fecha={fecha:yyyy-MM-dd}; Descripcion={captura.Descripcion}; EsOficial={captura.EsOficial}"), ct);

        return ResultadoOperacion.Ok($"Día festivo «{captura.Descripcion}» ({fecha:dd/MM/yyyy}) registrado correctamente.");
    }

    public async Task<ResultadoOperacion> CambiarEstatusAsync(int id, bool activar, string usuario, CancellationToken ct = default)
    {
        var ahora = DateTime.Now;
        var filas = await db.DiaFestivos
            .Where(d => d.ID == id && d.Estatus != activar)
            .ExecuteUpdateAsync(u => u
                .SetProperty(d => d.Estatus, activar)
                .SetProperty(d => d.update_time, ahora)
                .SetProperty(d => d.Update_ID_USUARIO, usuario), ct);

        if (filas == 0)
            return ResultadoOperacion.Advertencia("El registro no existe o ya tenía el estatus solicitado.");

        await auditoria.RegistrarAsync(new EventoAuditoria(
            activar ? "ACTIVACION" : "DESACTIVACION", "MM_DIAS_FESTIVOS", id.ToString(), true), ct);

        return ResultadoOperacion.Ok(activar ? "Día festivo activado." : "Día festivo desactivado.");
    }

    public async Task<ResultadoOperacion> EliminarLogicoAsync(int id, string usuario, CancellationToken ct = default)
    {
        var ahora = DateTime.Now;
        var filas = await db.DiaFestivos
            .Where(d => d.ID == id && d.Estatus)
            .ExecuteUpdateAsync(u => u
                .SetProperty(d => d.Estatus, false)
                .SetProperty(d => d.update_time, ahora)
                .SetProperty(d => d.Update_ID_USUARIO, usuario), ct);

        if (filas == 0)
            return ResultadoOperacion.Advertencia("El día festivo no existe o ya estaba inactivo.");

        await auditoria.RegistrarAsync(new EventoAuditoria("BAJA_LOGICA", "MM_DIAS_FESTIVOS", id.ToString(), true), ct);
        return ResultadoOperacion.Ok("Día festivo eliminado del catálogo activo.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IQueryable<DiaFestivoFila> Consulta(IQueryable<DiaFestivo> fuente) =>
        fuente.AsNoTracking()
              .OrderBy(d => d.Fecha)
              .Select(d => new DiaFestivoFila(
                  d.ID,
                  DateOnly.FromDateTime(d.Fecha),
                  d.Descripcion,
                  d.EsOficial,
                  d.Estatus,
                  d.update_time,
                  d.Update_ID_USUARIO));
}
