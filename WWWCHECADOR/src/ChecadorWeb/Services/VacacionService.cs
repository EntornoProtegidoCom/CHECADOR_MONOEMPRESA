using ChecadorWeb.Data;
using ChecadorWeb.Data.Entities;
using ChecadorWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ChecadorWeb.Services;

public interface IVacacionService
{
    Task<IReadOnlyList<VacacionFila>> ListarPorTrabajadorAsync(int idTrabajador, CancellationToken ct = default);
    Task<ResultadoOperacion> RegistrarAsync(int idTrabajador, VacacionAlta alta, string usuario, CancellationToken ct = default);
    Task<ResultadoOperacion> CancelarAsync(int idVacacion, int idTrabajador, string usuario, CancellationToken ct = default);
    Task<HashSet<DateOnly>> ObtenerVacacionesEnRangoAsync(int idTrabajador, DateOnly desde, DateOnly hasta, CancellationToken ct = default);
}

public sealed class VacacionService(ChecadorDbContext db, IAuditoria auditoria) : IVacacionService
{
    public async Task<IReadOnlyList<VacacionFila>> ListarPorTrabajadorAsync(int idTrabajador, CancellationToken ct = default)
    {
        return await db.MM_TRABAJADOR_VACACIONEs.AsNoTracking()
            .Where(v => v.IDTrabajador == idTrabajador)
            .OrderByDescending(v => v.FechaInicio)
            .Select(v => new VacacionFila(
                v.ID,
                v.IDTrabajador,
                DateOnly.FromDateTime(v.FechaInicio),
                DateOnly.FromDateTime(v.FechaFin),
                v.DiasTomados,
                v.PeriodoAnio,
                v.Observaciones,
                v.Estatus,
                v.create_time,
                v.Create_ID_USUARIO,
                v.update_time,
                v.Update_ID_USUARIO))
            .ToListAsync(ct);
    }

    public async Task<ResultadoOperacion> RegistrarAsync(int idTrabajador, VacacionAlta alta, string usuario, CancellationToken ct = default)
    {
        if (alta.FechaInicio is not { } inicio || alta.FechaFin is not { } fin)
            return ResultadoOperacion.Fallo("Las fechas de inicio y fin son obligatorias.");

        if (fin < inicio)
            return ResultadoOperacion.Fallo("La fecha de fin no puede ser anterior a la fecha de inicio.");

        if (alta.DiasTomados is not { } dias || dias <= 0)
            return ResultadoOperacion.Fallo("Debe indicar una cantidad válida de días tomados (mínimo 1).");

        // Validar existencia del colaborador
        var existeTrabajador = await db.MM_TRABAJADOREs.AnyAsync(t => t.IDTrabajador == idTrabajador, ct);
        if (!existeTrabajador)
            return ResultadoOperacion.Fallo("El empleado especificado no existe.");

        var inicioDt = inicio.ToDateTime(TimeOnly.MinValue);
        var finDt = fin.ToDateTime(TimeOnly.MinValue);

        // Validar solapamiento con otro periodo de vacaciones activo
        var solapado = await db.MM_TRABAJADOR_VACACIONEs.AnyAsync(v =>
            v.IDTrabajador == idTrabajador &&
            v.Estatus &&
            v.FechaInicio <= finDt &&
            v.FechaFin >= inicioDt, ct);

        if (solapado)
            return ResultadoOperacion.Advertencia("Existe un periodo de vacaciones activo que se cruza o solapa con las fechas indicadas.");

        var ahora = DateTime.Now;
        var entidad = new MM_TRABAJADOR_VACACIONE
        {
            IDTrabajador = idTrabajador,
            FechaInicio = inicioDt,
            FechaFin = finDt,
            DiasTomados = dias,
            PeriodoAnio = alta.PeriodoAnio ?? inicio.Year,
            Observaciones = string.IsNullOrWhiteSpace(alta.Observaciones) ? null : alta.Observaciones.Trim(),
            Estatus = true,
            create_time = ahora,
            Create_ID_USUARIO = usuario,
            update_time = ahora,
            Update_ID_USUARIO = usuario
        };

        db.MM_TRABAJADOR_VACACIONEs.Add(entidad);
        await db.SaveChangesAsync(ct);

        await auditoria.RegistrarAsync(new EventoAuditoria(
            "ALTA_VACACIONES",
            "MM_TRABAJADOR_VACACIONES",
            entidad.ID.ToString(),
            true,
            $"Trabajador={idTrabajador}; Rango={inicio:yyyy-MM-dd} al {fin:yyyy-MM-dd}; Dias={dias}; Periodo={entidad.PeriodoAnio}"), ct);

        return ResultadoOperacion.Ok($"Periodo de vacaciones registrado exitosamente ({inicio:dd/MM/yyyy} al {fin:dd/MM/yyyy}).");
    }

    public async Task<ResultadoOperacion> CancelarAsync(int idVacacion, int idTrabajador, string usuario, CancellationToken ct = default)
    {
        // El contexto usa NoTracking por defecto; sin AsTracking SaveChanges no persistiría el cambio.
        var vacacion = await db.MM_TRABAJADOR_VACACIONEs.AsTracking()
            .SingleOrDefaultAsync(v => v.ID == idVacacion && v.IDTrabajador == idTrabajador, ct);

        if (vacacion is null)
            return ResultadoOperacion.Fallo("El periodo vacacional no existe o no pertenece al empleado.");

        if (!vacacion.Estatus)
            return ResultadoOperacion.Advertencia("El periodo vacacional ya se encontraba cancelado.");

        var ahora = DateTime.Now;
        vacacion.Estatus = false;
        vacacion.update_time = ahora;
        vacacion.Update_ID_USUARIO = usuario;

        await db.SaveChangesAsync(ct);

        await auditoria.RegistrarAsync(new EventoAuditoria(
            "CANCELAR_VACACIONES",
            "MM_TRABAJADOR_VACACIONES",
            idVacacion.ToString(),
            true,
            $"Trabajador={idTrabajador}; CanceladaPor={usuario}"), ct);

        return ResultadoOperacion.Ok("Periodo de vacaciones cancelado correctamente.");
    }

    public async Task<HashSet<DateOnly>> ObtenerVacacionesEnRangoAsync(int idTrabajador, DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        var desdeDt = desde.ToDateTime(TimeOnly.MinValue);
        var hastaDt = hasta.ToDateTime(TimeOnly.MinValue);

        var periodos = await db.MM_TRABAJADOR_VACACIONEs.AsNoTracking()
            .Where(v => v.IDTrabajador == idTrabajador && v.Estatus && v.FechaInicio <= hastaDt && v.FechaFin >= desdeDt)
            .Select(v => new { v.FechaInicio, v.FechaFin })
            .ToListAsync(ct);

        var fechas = new HashSet<DateOnly>();
        foreach (var p in periodos)
        {
            var pInicio = DateOnly.FromDateTime(p.FechaInicio);
            var pFin = DateOnly.FromDateTime(p.FechaFin);

            var curr = pInicio < desde ? desde : pInicio;
            var max = pFin > hasta ? hasta : pFin;

            while (curr <= max)
            {
                fechas.Add(curr);
                curr = curr.AddDays(1);
            }
        }

        return fechas;
    }
}
