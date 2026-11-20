using ChecadorWeb.Data;
using ChecadorWeb.Data.Entities;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChecadorWeb.Services;

public interface IUsuarioService
{
    Task<PaginaResultado<UsuarioFila>> BuscarAsync(UsuarioFiltro filtro, CancellationToken ct = default);
    Task<UsuarioFila?> ObtenerAsync(string id, CancellationToken ct = default);
    Task<ResultadoOperacion> CrearAsync(UsuarioAlta alta, string actor, CancellationToken ct = default);
    Task<ResultadoOperacion> ActualizarAsync(string id, UsuarioEdicion datos, string actor, CancellationToken ct = default);
    Task<ResultadoOperacion> CambiarEstadoAsync(string id, bool activar, string actor, CancellationToken ct = default);
    Task<ResultadoOperacion> RestablecerPasswordAsync(string id, string nueva, string actor, CancellationToken ct = default);
    Task<ResultadoOperacion> CambiarPasswordPropiaAsync(string actor, string actual, string nueva, CancellationToken ct = default);
}

public sealed class UsuarioService(
    ChecadorDbContext db,
    IAuditoria auditoria,
    IPerfilCatalogo perfiles,
    IPermisoResolver permisos,
    IOptionsMonitor<SeguridadOptions> opciones) : IUsuarioService
{
    private string Vigente => opciones.CurrentValue.ValorVigente;
    private string Inactivo => opciones.CurrentValue.ValorInactivo;

    public async Task<PaginaResultado<UsuarioFila>> BuscarAsync(UsuarioFiltro filtro, CancellationToken ct = default)
    {
        var pagina = Paginacion.NormalizarPagina(filtro.Pagina);
        var tamano = Paginacion.NormalizarTamano(filtro.Tamano);
        var vigente = Vigente;

        var query = db.SS_USUARIOs.AsNoTracking();
        if (filtro.Perfil is { } perfil) query = query.Where(u => u.ID_PERFIL == perfil);
        query = filtro.Estado switch
        {
            "vigentes" => query.Where(u => u.ST_VIGENTE == vigente),
            "inactivos" => query.Where(u => u.ST_VIGENTE == null || u.ST_VIGENTE != vigente),
            _ => query,
        };

        foreach (var t in EmpleadoService.Terminos(filtro.Texto))
        {
            query = query.Where(u => u.ID_USUARIO.Contains(t)
                || (u.NB_NOMBRE != null && u.NB_NOMBRE.Contains(t))
                || (u.NB_PATERNO != null && u.NB_PATERNO.Contains(t))
                || (u.NB_MATERNO != null && u.NB_MATERNO.Contains(t))
                || (u.NB_EMAIL != null && u.NB_EMAIL.Contains(t)));
        }

        var total = await query.CountAsync(ct);
        var elementos = await query.OrderBy(u => u.ID_USUARIO)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(u => new UsuarioFila(u.ID_USUARIO, u.ID_PERFIL, u.NB_NOMBRE, u.NB_PATERNO, u.NB_MATERNO, u.NB_EMAIL, u.ST_VIGENTE))
            .ToListAsync(ct);

        return new PaginaResultado<UsuarioFila> { Elementos = elementos, Pagina = pagina, TamanoPagina = tamano, Total = total };
    }

    public Task<UsuarioFila?> ObtenerAsync(string id, CancellationToken ct = default) =>
        db.SS_USUARIOs.AsNoTracking()
            .Where(u => u.ID_USUARIO == id)
            .Select(u => new UsuarioFila(u.ID_USUARIO, u.ID_PERFIL, u.NB_NOMBRE, u.NB_PATERNO, u.NB_MATERNO, u.NB_EMAIL, u.ST_VIGENTE))
            .SingleOrDefaultAsync(ct);

    public async Task<ResultadoOperacion> CrearAsync(UsuarioAlta alta, string actor, CancellationToken ct = default)
    {
        var id = alta.IdUsuario.Trim();
        if (!await PerfilExisteAsync(alta.IdPerfil, ct)) return ResultadoOperacion.Fallo("El perfil seleccionado no existe.");
        if (await db.SS_USUARIOs.AsNoTracking().AnyAsync(u => u.ID_USUARIO == id, ct))
            return ResultadoOperacion.Advertencia("Ya existe un usuario con ese identificador.");

        db.SS_USUARIOs.Add(new SS_USUARIO
        {
            ID_USUARIO = id,
            ID_PERFIL = alta.IdPerfil!.Value,
            NB_NOMBRE = alta.Nombre.Trim(),
            NB_PATERNO = Limpiar(alta.Paterno),
            NB_MATERNO = Limpiar(alta.Materno),
            NB_EMAIL = Limpiar(alta.Email),
            TX_PASSWORD = LegacyPasswordHasher.Hash(alta.Password),
            ST_VIGENTE = Vigente,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
        {
            return ResultadoOperacion.Advertencia("Ya existe un usuario con ese identificador.");
        }
        finally
        {
            db.ChangeTracker.Clear();
        }

        await auditoria.RegistrarAsync(new EventoAuditoria("ALTA", "SS_USUARIOS", id, true, $"Perfil={alta.IdPerfil}"), ct);
        return ResultadoOperacion.Ok($"Usuario {id} creado.");
    }

    public async Task<ResultadoOperacion> ActualizarAsync(string id, UsuarioEdicion datos, string actor, CancellationToken ct = default)
    {
        var actual = await ObtenerAsync(id, ct);
        if (actual is null) return ResultadoOperacion.Fallo("El usuario no existe.");
        if (!await PerfilExisteAsync(datos.IdPerfil, ct)) return ResultadoOperacion.Fallo("El perfil seleccionado no existe.");

        var nuevoPerfil = datos.IdPerfil!.Value;
        if (nuevoPerfil != actual.Perfil)
        {
            if (string.Equals(id, actor, StringComparison.OrdinalIgnoreCase))
                return ResultadoOperacion.Advertencia("No puede cambiar su propio perfil.");
            if (actual.Vigente == Vigente && EsAdministrador(actual.Perfil) && !EsAdministrador(nuevoPerfil)
                && !await ExisteOtroAdministradorAsync(id, ct))
                return ResultadoOperacion.Advertencia("No se puede quitar el último usuario vigente con permiso para administrar usuarios.");
        }

        await db.SS_USUARIOs.Where(u => u.ID_USUARIO == id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(x => x.ID_PERFIL, nuevoPerfil)
                .SetProperty(x => x.NB_NOMBRE, datos.Nombre.Trim())
                .SetProperty(x => x.NB_PATERNO, Limpiar(datos.Paterno))
                .SetProperty(x => x.NB_MATERNO, Limpiar(datos.Materno))
                .SetProperty(x => x.NB_EMAIL, Limpiar(datos.Email)), ct);

        await auditoria.RegistrarAsync(new EventoAuditoria("EDICION", "SS_USUARIOS", id, true,
            nuevoPerfil != actual.Perfil ? $"Perfil {actual.Perfil} -> {nuevoPerfil}" : null), ct);
        return ResultadoOperacion.Ok("Usuario actualizado.");
    }

    public async Task<ResultadoOperacion> CambiarEstadoAsync(string id, bool activar, string actor, CancellationToken ct = default)
    {
        var actual = await ObtenerAsync(id, ct);
        if (actual is null) return ResultadoOperacion.Fallo("El usuario no existe.");
        var estaVigente = actual.Vigente == Vigente;
        if (estaVigente == activar)
            return ResultadoOperacion.Advertencia(activar ? "El usuario ya estaba vigente." : "El usuario ya estaba inactivo.");

        if (!activar)
        {
            if (string.Equals(id, actor, StringComparison.OrdinalIgnoreCase))
                return ResultadoOperacion.Advertencia("No puede desactivar su propio usuario.");
            if (EsAdministrador(actual.Perfil) && !await ExisteOtroAdministradorAsync(id, ct))
                return ResultadoOperacion.Advertencia("No se puede desactivar el último usuario vigente con permiso para administrar usuarios.");
        }

        var nuevo = activar ? Vigente : Inactivo;
        await db.SS_USUARIOs.Where(u => u.ID_USUARIO == id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.ST_VIGENTE, nuevo), ct);

        await auditoria.RegistrarAsync(new EventoAuditoria(activar ? "ACTIVACION" : "DESACTIVACION", "SS_USUARIOS", id, true), ct);
        return ResultadoOperacion.Ok(activar ? "Usuario activado." : "Usuario desactivado. Sus sesiones abiertas se cerrarán en su siguiente petición.");
    }

    public async Task<ResultadoOperacion> RestablecerPasswordAsync(string id, string nueva, string actor, CancellationToken ct = default)
    {
        var filas = await db.SS_USUARIOs.Where(u => u.ID_USUARIO == id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.TX_PASSWORD, LegacyPasswordHasher.Hash(nueva)), ct);
        if (filas == 0) return ResultadoOperacion.Fallo("El usuario no existe.");

        await auditoria.RegistrarAsync(new EventoAuditoria("RESTABLECER_PASSWORD", "SS_USUARIOS", id, true), ct);
        return ResultadoOperacion.Ok("Contraseña restablecida. Las sesiones abiertas de ese usuario se cerrarán.");
    }

    public async Task<ResultadoOperacion> CambiarPasswordPropiaAsync(string actor, string actual, string nueva, CancellationToken ct = default)
    {
        var hash = await db.SS_USUARIOs.AsNoTracking()
            .Where(u => u.ID_USUARIO == actor).Select(u => u.TX_PASSWORD).SingleOrDefaultAsync(ct);
        if (!LegacyPasswordHasher.Verify(actual, hash))
        {
            await auditoria.RegistrarAsync(new EventoAuditoria("CAMBIO_PASSWORD_PROPIO", "SS_USUARIOS", actor, false, "Contraseña actual incorrecta"), ct);
            return ResultadoOperacion.Fallo("La contraseña actual no es correcta.");
        }
        if (actual == nueva) return ResultadoOperacion.Advertencia("La nueva contraseña debe ser distinta de la actual.");

        await db.SS_USUARIOs.Where(u => u.ID_USUARIO == actor)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.TX_PASSWORD, LegacyPasswordHasher.Hash(nueva)), ct);
        await auditoria.RegistrarAsync(new EventoAuditoria("CAMBIO_PASSWORD_PROPIO", "SS_USUARIOS", actor, true), ct);
        return ResultadoOperacion.Ok("Contraseña actualizada. Inicie sesión de nuevo.");
    }

    private bool EsAdministrador(int perfil) => permisos.Tiene(perfil, Permisos.UsuariosEditar);

    private async Task<bool> ExisteOtroAdministradorAsync(string excluir, CancellationToken ct)
    {
        var perfilesAdmin = opciones.CurrentValue.PermisosPorPerfil.Keys
            .Select(k => int.TryParse(k, out var p) ? p : (int?)null)
            .Where(p => p is not null && EsAdministrador(p.Value))
            .Select(p => p!.Value)
            .ToList();
        var vigente = Vigente;
        return await db.SS_USUARIOs.AsNoTracking()
            .AnyAsync(u => u.ID_USUARIO != excluir && u.ST_VIGENTE == vigente && perfilesAdmin.Contains(u.ID_PERFIL), ct);
    }

    private async Task<bool> PerfilExisteAsync(int? perfil, CancellationToken ct) =>
        perfil is { } p && (await perfiles.ObtenerAsync(ct)).Any(x => x.Id == p);

    private static string? Limpiar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
