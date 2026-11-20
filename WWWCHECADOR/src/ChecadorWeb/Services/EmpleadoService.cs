using System.Data;
using ChecadorWeb.Data;
using ChecadorWeb.Data.Entities;
using ChecadorWeb.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChecadorWeb.Services;

public interface IEmpleadoService
{
    Task<PaginaResultado<EmpleadoFila>> BuscarAsync(EmpleadoFiltro filtro, CancellationToken ct = default);
    Task<EmpleadoEdicion?> ObtenerEdicionAsync(int id, CancellationToken ct = default);
    Task<EmpleadoResumen?> ObtenerResumenAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<EmpleadoOpcion>> ListarOpcionesAsync(bool soloActivos, CancellationToken ct = default);
    Task<(ResultadoOperacion Resultado, int? Id)> CrearAsync(EmpleadoAlta alta, string usuario, CancellationToken ct = default);
    Task<ResultadoOperacion> ActualizarAsync(int id, EmpleadoEdicion datos, string usuario, CancellationToken ct = default);
    Task<ResultadoOperacion> CambiarEstadoAsync(int id, bool activar, string usuario, CancellationToken ct = default);
    Task<HorarioEdicion?> ObtenerHorarioAsync(int id, CancellationToken ct = default);
    Task<ResultadoOperacion> ActualizarHorarioAsync(int id, HorarioEdicion horario, string usuario, CancellationToken ct = default);
}

public sealed class EmpleadoService(
    ChecadorDbContext db,
    IAuditoria auditoria,
    IOptionsMonitor<HorariosOptions> horariosOpciones) : IEmpleadoService
{
    public const string CodigoIdTrabajadorDuplicado = "ID_TRABAJADOR_DUPLICADO";
    private const string Coleccion = "Modern_Spanish_CI_AI";

    public async Task<PaginaResultado<EmpleadoFila>> BuscarAsync(EmpleadoFiltro filtro, CancellationToken ct = default)
    {
        var pagina = Paginacion.NormalizarPagina(filtro.Pagina);
        var tamano = Paginacion.NormalizarTamano(filtro.Tamano);

        var query = db.MM_TRABAJADOREs.AsNoTracking();

        query = filtro.Estado switch
        {
            "activos" => query.Where(t => t.status == true),
            "inactivos" => query.Where(t => t.status != true),
            _ => query,
        };
        if (filtro.SinHorario) query = query.Where(t => t.HoraEntrada == null || t.HoraSalida == null);

        foreach (var termino in Terminos(filtro.Texto))
        {
            if (int.TryParse(termino, out var id))
            {
                query = query.Where(t => t.IDTrabajador == id
                    || EF.Functions.Collate(t.Nombre, Coleccion).Contains(termino)
                    || EF.Functions.Collate(t.ApellidoP, Coleccion).Contains(termino)
                    || EF.Functions.Collate(t.ApellidoM, Coleccion).Contains(termino));
            }
            else
            {
                query = query.Where(t => EF.Functions.Collate(t.Nombre, Coleccion).Contains(termino)
                    || EF.Functions.Collate(t.ApellidoP, Coleccion).Contains(termino)
                    || EF.Functions.Collate(t.ApellidoM, Coleccion).Contains(termino)
                    || (t.Correo != null && EF.Functions.Collate(t.Correo, Coleccion).Contains(termino)));
            }
        }

        var total = await query.CountAsync(ct);
        var elementos = await query
            .OrderBy(t => t.ApellidoP).ThenBy(t => t.ApellidoM).ThenBy(t => t.Nombre).ThenBy(t => t.IDTrabajador)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(t => new EmpleadoFila(
                t.IDTrabajador, t.Nombre, t.ApellidoP, t.ApellidoM, t.Correo, t.status,
                t.DD_EQUIPO_VINCULOs.Count(v => v.Activo),
                t.HoraEntrada, t.HoraSalidaComida, t.HoraEntradaComida, t.HoraSalida,
                t.update_time, t.Update_ID_USUARIO))
            .ToListAsync(ct);

        return new PaginaResultado<EmpleadoFila> { Elementos = elementos, Pagina = pagina, TamanoPagina = tamano, Total = total };
    }

    public Task<EmpleadoEdicion?> ObtenerEdicionAsync(int id, CancellationToken ct = default) =>
        db.MM_TRABAJADOREs.AsNoTracking()
            .Where(t => t.IDTrabajador == id)
            .Select(t => new EmpleadoEdicion
            {
                Nombre = t.Nombre,
                ApellidoP = t.ApellidoP,
                ApellidoM = t.ApellidoM,
                Correo = t.Correo,
                Activo = t.status == true,
                Sello = t.update_time.HasValue ? t.update_time.Value.Ticks : null,
            })
            .SingleOrDefaultAsync(ct);

    public Task<EmpleadoResumen?> ObtenerResumenAsync(int id, CancellationToken ct = default) =>
        db.MM_TRABAJADOREs.AsNoTracking()
            .Where(t => t.IDTrabajador == id)
            .Select(t => new EmpleadoResumen(
                t.IDTrabajador,
                t.ApellidoP + " " + t.ApellidoM + " " + t.Nombre,
                t.status,
                t.DD_EQUIPO_VINCULOs.Count(v => v.Activo),
                t.update_time,
                t.Update_ID_USUARIO))
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<EmpleadoOpcion>> ListarOpcionesAsync(bool soloActivos, CancellationToken ct = default) =>
        await db.MM_TRABAJADOREs.AsNoTracking()
            .Where(t => !soloActivos || t.status == true)
            .OrderBy(t => t.ApellidoP).ThenBy(t => t.ApellidoM).ThenBy(t => t.Nombre)
            .Select(t => new EmpleadoOpcion(t.IDTrabajador, t.ApellidoP + " " + t.ApellidoM + " " + t.Nombre, t.status == true))
            .ToListAsync(ct);

    public async Task<(ResultadoOperacion Resultado, int? Id)> CrearAsync(EmpleadoAlta alta, string usuario, CancellationToken ct = default)
    {
        if (alta.IDTrabajador is not > 0)
            return (ResultadoOperacion.Fallo("El ID del trabajador debe ser un entero mayor que cero."), null);

        var idTrabajador = alta.IDTrabajador.Value;
        var pin = int.Parse(alta.Pin);
        var horario = alta.Horario;
        if (!horario.EstaVacio)
        {
            var errores = HorarioValidator.Validar(horario.ACaptura(), horariosOpciones.CurrentValue);
            if (errores.Count > 0) return (ResultadoOperacion.Fallo(errores[0].Mensaje), null);
        }

        await using var tx = await db.Database.IniciarTransaccionAsync(IsolationLevel.ReadCommitted, ct);

        if (await db.MM_TRABAJADOREs.AnyAsync(t => t.IDTrabajador == idTrabajador, ct))
            return (new ResultadoOperacion(TipoResultado.Error, $"Ya existe un empleado con el ID {idTrabajador}.", CodigoIdTrabajadorDuplicado), null);

        var ahora = DateTime.Now;
        var entidad = new MM_TRABAJADORE
        {
            IDTrabajador = idTrabajador,
            Nombre = alta.Nombre.Trim(),
            ApellidoP = alta.ApellidoP.Trim(),
            ApellidoM = alta.ApellidoM.Trim(),
            Correo = string.IsNullOrWhiteSpace(alta.Correo) ? null : alta.Correo.Trim(),
            PIN = pin,
            // Formato heredado de sp_catalogo_trabajadores; no cambiar sin plan aprobado.
            Contrasena = "URGL" + pin.ToString(System.Globalization.CultureInfo.InvariantCulture),
            status = alta.Activo,
            create_time = ahora,
            Create_ID_USUARIO = usuario,
            update_time = ahora,
            Update_ID_USUARIO = usuario,
            HoraEntrada = horario.Entrada,
            HoraSalidaComida = horario.SalidaComida,
            HoraEntradaComida = horario.EntradaComida,
            HoraSalida = horario.Salida,
        };

        db.MM_TRABAJADOREs.Add(entidad);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return (new ResultadoOperacion(TipoResultado.Error, $"Ya existe un empleado con el ID {idTrabajador}.", CodigoIdTrabajadorDuplicado), null);
        }

        await tx.ConfirmarAsync(ct);
        db.ChangeTracker.Clear();

        await auditoria.RegistrarAsync(new EventoAuditoria("ALTA", "MM_TRABAJADORES", idTrabajador.ToString(), true,
            $"Nombre={entidad.Nombre} {entidad.ApellidoP} {entidad.ApellidoM}; Activo={alta.Activo}; Horario={(horario.EstaVacio ? "sin capturar" : "capturado")}"), ct);

        return (ResultadoOperacion.Ok($"Empleado {idTrabajador} creado."), idTrabajador);
    }

    public async Task<ResultadoOperacion> ActualizarAsync(int id, EmpleadoEdicion datos, string usuario, CancellationToken ct = default)
    {
        var actual = await ObtenerResumenAsync(id, ct);
        if (actual is null) return ResultadoOperacion.Fallo("El empleado no existe.");

        if (!datos.Activo && actual.Activo == true && actual.VinculosActivos > 0)
            return ResultadoOperacion.Advertencia($"El empleado tiene {actual.VinculosActivos} vínculo(s) de equipo activo(s). Desvincúlelos antes de desactivarlo.");

        DateTime? sello = datos.Sello is { } s ? new DateTime(s) : null;
        var nuevoPin = string.IsNullOrWhiteSpace(datos.NuevoPin) ? (int?)null : int.Parse(datos.NuevoPin);
        var correo = string.IsNullOrWhiteSpace(datos.Correo) ? null : datos.Correo.Trim();
        var ahora = DateTime.Now;

        // ExecuteUpdate solo toca las columnas permitidas; la condición sobre update_time evita sobrescribir cambios ajenos.
        var filas = await db.MM_TRABAJADOREs
            .Where(t => t.IDTrabajador == id && t.update_time == sello)
            .ExecuteUpdateAsync(u => u
                .SetProperty(t => t.Nombre, datos.Nombre.Trim())
                .SetProperty(t => t.ApellidoP, datos.ApellidoP.Trim())
                .SetProperty(t => t.ApellidoM, datos.ApellidoM.Trim())
                .SetProperty(t => t.Correo, correo)
                .SetProperty(t => t.status, datos.Activo)
                .SetProperty(t => t.PIN, t => nuevoPin ?? t.PIN)
                .SetProperty(t => t.update_time, ahora)
                .SetProperty(t => t.Update_ID_USUARIO, usuario), ct);

        if (filas == 0)
            return ResultadoOperacion.Advertencia("Otro usuario modificó este empleado mientras lo editaba. Recargue la página y vuelva a intentar.");

        await auditoria.RegistrarAsync(new EventoAuditoria("EDICION", "MM_TRABAJADORES", id.ToString(), true,
            $"Activo={datos.Activo}; PIN cambiado={(nuevoPin.HasValue ? "sí" : "no")}"), ct);
        return ResultadoOperacion.Ok("Empleado actualizado.");
    }

    public async Task<ResultadoOperacion> CambiarEstadoAsync(int id, bool activar, string usuario, CancellationToken ct = default)
    {
        var actual = await ObtenerResumenAsync(id, ct);
        if (actual is null) return ResultadoOperacion.Fallo("El empleado no existe.");
        if ((actual.Activo == true) == activar)
            return ResultadoOperacion.Advertencia(activar ? "El empleado ya estaba activo." : "El empleado ya estaba inactivo.");
        if (!activar && actual.VinculosActivos > 0)
            return ResultadoOperacion.Advertencia($"El empleado tiene {actual.VinculosActivos} vínculo(s) de equipo activo(s). Desvincúlelos antes de desactivarlo.");

        var ahora = DateTime.Now;
        var filas = await db.MM_TRABAJADOREs
            .Where(t => t.IDTrabajador == id && t.update_time == actual.Actualizado)
            .ExecuteUpdateAsync(u => u
                .SetProperty(t => t.status, activar)
                .SetProperty(t => t.update_time, ahora)
                .SetProperty(t => t.Update_ID_USUARIO, usuario), ct);

        if (filas == 0) return ResultadoOperacion.Advertencia("El empleado cambió mientras se procesaba la solicitud. Intente de nuevo.");

        await auditoria.RegistrarAsync(new EventoAuditoria(activar ? "ACTIVACION" : "DESACTIVACION", "MM_TRABAJADORES", id.ToString(), true), ct);
        return ResultadoOperacion.Ok(activar ? "Empleado activado." : "Empleado desactivado.");
    }

    public Task<HorarioEdicion?> ObtenerHorarioAsync(int id, CancellationToken ct = default) =>
        db.MM_TRABAJADOREs.AsNoTracking()
            .Where(t => t.IDTrabajador == id)
            .Select(t => new HorarioEdicion
            {
                Entrada = t.HoraEntrada,
                SalidaComida = t.HoraSalidaComida,
                EntradaComida = t.HoraEntradaComida,
                Salida = t.HoraSalida,
                Sello = t.update_time.HasValue ? t.update_time.Value.Ticks : null,
            })
            .SingleOrDefaultAsync(ct);

    public async Task<ResultadoOperacion> ActualizarHorarioAsync(int id, HorarioEdicion horario, string usuario, CancellationToken ct = default)
    {
        var errores = HorarioValidator.Validar(horario.ACaptura(), horariosOpciones.CurrentValue);
        if (errores.Count > 0) return ResultadoOperacion.Fallo(errores[0].Mensaje);

        var anterior = await ObtenerHorarioAsync(id, ct);
        if (anterior is null) return ResultadoOperacion.Fallo("El empleado no existe.");

        DateTime? sello = horario.Sello is { } s ? new DateTime(s) : null;
        var ahora = DateTime.Now;
        var filas = await db.MM_TRABAJADOREs
            .Where(t => t.IDTrabajador == id && t.update_time == sello)
            .ExecuteUpdateAsync(u => u
                .SetProperty(t => t.HoraEntrada, horario.Entrada)
                .SetProperty(t => t.HoraSalidaComida, horario.SalidaComida)
                .SetProperty(t => t.HoraEntradaComida, horario.EntradaComida)
                .SetProperty(t => t.HoraSalida, horario.Salida)
                .SetProperty(t => t.update_time, ahora)
                .SetProperty(t => t.Update_ID_USUARIO, usuario), ct);

        if (filas == 0)
            return ResultadoOperacion.Advertencia("Otro usuario modificó este empleado mientras editaba el horario. Recargue la página y vuelva a intentar.");

        await auditoria.RegistrarAsync(new EventoAuditoria("EDICION_HORARIO", "MM_TRABAJADORES", id.ToString(), true,
            $"Antes={Formato(anterior)}; Después={Formato(horario)}"), ct);
        return ResultadoOperacion.Ok("Horario actualizado.");
    }

    private static string Formato(HorarioEdicion h) =>
        $"{h.Entrada:HH\\:mm}-{h.SalidaComida:HH\\:mm}-{h.EntradaComida:HH\\:mm}-{h.Salida:HH\\:mm}";

    internal static IEnumerable<string> Terminos(string? texto) =>
        (texto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(5)
            .Select(t => t.Length > 60 ? t[..60] : t);
}
