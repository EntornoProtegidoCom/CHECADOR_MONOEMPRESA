using System.Data;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using ChecadorWeb.Tests.Web;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ChecadorWeb.Tests.Integracion;

/// <summary>
/// Pruebas contra DB_CHECADOR_ENT. Cada prueba trabaja en una transacción revertida (autorizado por el usuario).
/// </summary>
public class ServiciosIntegracionTests
{
    private const string Actor = ContextoRevertido.Actor;
    private const string EquipoPrueba = "QA-WEB-TEST-01";

    private static void RequiereBd() => Skip.IfNot(ChecadorWebFactory.HayBaseDatos, "Sin acceso a la base de datos de pruebas.");

    private static async Task<EmpleadoAlta> NuevoEmpleado(ContextoRevertido c, string pin = "4321", bool activo = true, int? id = null)
    {
        id ??= await c.Db.MM_TRABAJADOREs.Select(t => t.IDTrabajador).DefaultIfEmpty().MaxAsync() + 1;
        return new EmpleadoAlta
        {
            IDTrabajador = id,
            Nombre = "QA", ApellidoP = "PRUEBA", ApellidoM = "WEB", Correo = "qa@example.com",
            Pin = pin, ConfirmarPin = pin, Activo = activo,
            Horario = new HorarioEdicion
            {
                Entrada = new TimeOnly(8, 0), SalidaComida = new TimeOnly(13, 0),
                EntradaComida = new TimeOnly(14, 0), Salida = new TimeOnly(17, 0),
            },
        };
    }

    [SkippableFact]
    public async Task Alta_de_empleado_replica_sp_catalogo_y_es_aceptada_por_sp_login_trabajador()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        var maximo = await c.Db.MM_TRABAJADOREs.MaxAsync(t => t.IDTrabajador);
        var idSolicitado = maximo + 7;

        var (r, id) = await c.Empleados.CrearAsync(await NuevoEmpleado(c, id: idSolicitado), Actor);

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(idSolicitado, id);
        var e = await c.Db.MM_TRABAJADOREs.SingleAsync(t => t.IDTrabajador == id);
        Assert.Equal("URGL4321", e.Contrasena);
        Assert.Equal(4321, e.PIN);
        Assert.True(e.status);
        Assert.Equal(Actor, e.Create_ID_USUARIO);
        Assert.Equal(Actor, e.Update_ID_USUARIO);
        Assert.Equal(new TimeOnly(8, 0), e.HoraEntrada);

        var ds = await c.EjecutarSpXmlAsync("sp_desktop_login_trabajador", "C", ("IDTrabajador", id), ("PIN", 4321));
        Assert.Equal(1, Convert.ToInt32(ds.Tables[0].Rows[0]["EXITO"]));

        var evento = Assert.Single(c.Auditoria.Eventos);
        Assert.Equal("ALTA", evento.Accion);
        Assert.DoesNotContain("4321", evento.Detalle);
    }

    [SkippableFact]
    public async Task Alta_de_empleado_rechaza_ID_duplicado()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        var alta = await NuevoEmpleado(c);
        var (primera, id) = await c.Empleados.CrearAsync(alta, Actor);
        Assert.True(primera.Exitoso, primera.Mensaje);

        var duplicado = await NuevoEmpleado(c, pin: "5678", id: id);
        var (resultado, idDuplicado) = await c.Empleados.CrearAsync(duplicado, Actor);

        Assert.False(resultado.Exitoso);
        Assert.Equal(EmpleadoService.CodigoIdTrabajadorDuplicado, resultado.Codigo);
        Assert.Contains($"ID {id}", resultado.Mensaje);
        Assert.Null(idDuplicado);
    }

    [SkippableFact]
    public async Task Edicion_concurrente_de_empleado_se_detecta_y_no_sobrescribe()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        var (_, id) = await c.Empleados.CrearAsync(await NuevoEmpleado(c), Actor);

        var edicionA = (await c.Empleados.ObtenerEdicionAsync(id!.Value))!;
        var edicionB = (await c.Empleados.ObtenerEdicionAsync(id.Value))!;
        await Task.Delay(20);

        edicionA.Nombre = "PRIMERO";
        Assert.True((await c.Empleados.ActualizarAsync(id.Value, edicionA, Actor)).Exitoso);

        edicionB.Nombre = "SEGUNDO";
        var r = await c.Empleados.ActualizarAsync(id.Value, edicionB, Actor);
        Assert.Equal(TipoResultado.Advertencia, r.Tipo);
        Assert.Equal("PRIMERO", await c.Db.MM_TRABAJADOREs.Where(t => t.IDTrabajador == id).Select(t => t.Nombre).SingleAsync());
    }

    [SkippableFact]
    public async Task Cambio_de_pin_conserva_formato_y_no_toca_contrasena()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        var (_, id) = await c.Empleados.CrearAsync(await NuevoEmpleado(c, "4321"), Actor);
        var edicion = (await c.Empleados.ObtenerEdicionAsync(id!.Value))!;
        edicion.NuevoPin = edicion.ConfirmarNuevoPin = "5678";

        Assert.True((await c.Empleados.ActualizarAsync(id.Value, edicion, Actor)).Exitoso);

        var e = await c.Db.MM_TRABAJADOREs.SingleAsync(t => t.IDTrabajador == id);
        Assert.Equal(5678, e.PIN);
        Assert.Equal("URGL4321", e.Contrasena); // sp_catalogo_trabajadores (U) tampoco la modifica.
    }

    [SkippableFact]
    public async Task Empleado_con_vinculo_activo_no_se_puede_desactivar()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        var (_, id) = await c.Empleados.CrearAsync(await NuevoEmpleado(c), Actor);
        Assert.True((await c.Vinculos.CrearAsync(new VinculoAlta { NbEquipo = EquipoPrueba, IdTrabajador = id }, Actor)).Exitoso);

        var r = await c.Empleados.CambiarEstadoAsync(id!.Value, activar: false, Actor);
        Assert.Equal(TipoResultado.Advertencia, r.Tipo);

        var vinculoId = await c.Db.DD_EQUIPO_VINCULOs.Where(v => v.NBEquipo == EquipoPrueba && v.Activo).Select(v => v.ID).SingleAsync();
        Assert.True((await c.Vinculos.DesactivarAsync(vinculoId, Actor)).Exitoso);
        Assert.True((await c.Empleados.CambiarEstadoAsync(id.Value, activar: false, Actor)).Exitoso);
    }

    [SkippableFact]
    public async Task Equipo_solo_admite_un_vinculo_activo_y_es_visible_para_sp_vinculo_equipo()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        var (_, a) = await c.Empleados.CrearAsync(await NuevoEmpleado(c, "1111"), Actor);
        var (_, b) = await c.Empleados.CrearAsync(await NuevoEmpleado(c, "2222"), Actor);

        Assert.True((await c.Vinculos.CrearAsync(new VinculoAlta { NbEquipo = EquipoPrueba, IdTrabajador = a }, Actor)).Exitoso);
        var duplicado = await c.Vinculos.CrearAsync(new VinculoAlta { NbEquipo = EquipoPrueba.ToLowerInvariant(), IdTrabajador = b }, Actor);
        Assert.Equal(TipoResultado.Advertencia, duplicado.Tipo);

        var ds = await c.EjecutarSpXmlAsync("sp_desktop_vinculo_equipo", "C", ("NBEquipo", EquipoPrueba));
        Assert.Equal(a, Convert.ToInt32(ds.Tables[1].Rows[0]["IDTrabajador"]));

        var v = await c.Db.DD_EQUIPO_VINCULOs.SingleAsync(x => x.NBEquipo == EquipoPrueba);
        Assert.Equal(Actor, v.Create_ID_USUARIO);
        Assert.Equal(Actor, v.Update_ID_USUARIO);
    }

    [SkippableFact]
    public async Task Vinculo_con_empleado_inactivo_es_rechazado()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        var (_, id) = await c.Empleados.CrearAsync(await NuevoEmpleado(c, activo: false), Actor);
        var r = await c.Vinculos.CrearAsync(new VinculoAlta { NbEquipo = EquipoPrueba, IdTrabajador = id }, Actor);
        Assert.Equal(TipoResultado.Advertencia, r.Tipo);
        Assert.False(await c.Db.DD_EQUIPO_VINCULOs.AnyAsync(v => v.NBEquipo == EquipoPrueba && v.Activo));
    }

    [SkippableFact]
    public async Task Segundo_equipo_para_el_mismo_empleado_requiere_confirmacion()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        var (_, id) = await c.Empleados.CrearAsync(await NuevoEmpleado(c), Actor);
        await c.Vinculos.CrearAsync(new VinculoAlta { NbEquipo = EquipoPrueba, IdTrabajador = id }, Actor);

        var sinConfirmar = await c.Vinculos.CrearAsync(new VinculoAlta { NbEquipo = "QA-WEB-TEST-02", IdTrabajador = id }, Actor);
        Assert.Equal(VinculoService.CodigoConfirmarMultiple, sinConfirmar.Codigo);

        var confirmado = await c.Vinculos.CrearAsync(new VinculoAlta { NbEquipo = "QA-WEB-TEST-02", IdTrabajador = id, ConfirmarMultiple = true }, Actor);
        Assert.True(confirmado.Exitoso);
        Assert.Contains(await c.Vinculos.DetectarConflictosAsync(), x => x.Tipo == TipoConflicto.TrabajadorConVariosEquipos);
    }

    [SkippableFact]
    public async Task Heartbeat_de_otro_trabajador_se_reporta_como_conflicto_y_en_estaciones()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        var (_, a) = await c.Empleados.CrearAsync(await NuevoEmpleado(c, "1111"), Actor);
        var (_, b) = await c.Empleados.CrearAsync(await NuevoEmpleado(c, "2222"), Actor);
        await c.Vinculos.CrearAsync(new VinculoAlta { NbEquipo = EquipoPrueba, IdTrabajador = a }, Actor);
        await c.EjecutarAsync("EXEC dbo.sp_desktop_registrar_heartbeat @e, @t, '127.0.0.1', 'qa'",
            new SqlParameter("@e", EquipoPrueba), new SqlParameter("@t", b));

        var conflictos = await c.Vinculos.DetectarConflictosAsync();
        Assert.Contains(conflictos, x => x.Tipo == TipoConflicto.HeartbeatTrabajadorDistinto && x.NbEquipo == EquipoPrueba);

        var estaciones = await c.Estaciones.ObtenerAsync(null, null);
        var fila = Assert.Single(estaciones.Filas, f => f.NbEquipo == EquipoPrueba);
        Assert.Equal(EstadoComunicacion.EnLinea, fila.Estado);
        Assert.Equal("ACTIVO", fila.EstadoInformado);
        Assert.NotNull(fila.Observacion);
    }

    [SkippableFact]
    public async Task Horario_invalido_no_se_guarda()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        var (_, id) = await c.Empleados.CrearAsync(await NuevoEmpleado(c), Actor);
        var h = (await c.Empleados.ObtenerHorarioAsync(id!.Value))!;
        h.Entrada = new TimeOnly(22, 0);
        h.SalidaComida = h.EntradaComida = null;
        h.Salida = new TimeOnly(6, 0);

        var r = await c.Empleados.ActualizarHorarioAsync(id.Value, h, Actor);

        Assert.Equal(TipoResultado.Error, r.Tipo);
        Assert.Equal(new TimeOnly(8, 0), (await c.Empleados.ObtenerHorarioAsync(id.Value))!.Entrada);
    }

    [SkippableFact]
    public async Task Usuario_creado_por_el_sitio_es_valido_para_los_sp_de_seguridad_existentes()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        const string id = "qa.web.tmp";
        const string password = "Prueba#2026";

        var r = await c.Usuarios.CrearAsync(new UsuarioAlta
        {
            IdUsuario = id, IdPerfil = 1, Nombre = "QA", Paterno = "WEB", Password = password, ConfirmarPassword = password,
        }, Actor);
        Assert.True(r.Exitoso, r.Mensaje);

        var seguridad = await c.EjecutarSpXmlAsync("sp_seguridad_usuarios", "C", ("ID_USUARIO", id), ("Password", password));
        Assert.Equal(1, Convert.ToInt32(seguridad.Tables[0].Rows[0]["IsValid"]));

        var admin = await c.EjecutarSpXmlAsync("sp_desktop_login_admin", "C", ("ID_USUARIO", id), ("Password", password));
        Assert.Equal(1, Convert.ToInt32(admin.Tables[0].Rows[0]["EXITO"]));

        var login = await c.Autenticacion.ValidarAsync(id, password);
        Assert.Equal(ResultadoLogin.Exito, login.Resultado);
        Assert.Equal(ResultadoLogin.CredencialesInvalidas, (await c.Autenticacion.ValidarAsync(id, "Incorrecta#1")).Resultado);
    }

    [SkippableFact]
    public async Task Usuario_desactivado_no_inicia_sesion_ni_pasa_sp_login_admin()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        const string id = "qa.web.tmp";
        const string password = "Prueba#2026";
        await c.Usuarios.CrearAsync(new UsuarioAlta { IdUsuario = id, IdPerfil = 1, Nombre = "QA", Password = password, ConfirmarPassword = password }, Actor);

        Assert.True((await c.Usuarios.CambiarEstadoAsync(id, activar: false, actor: Actor)).Exitoso);

        Assert.Equal("I", await c.Db.SS_USUARIOs.Where(u => u.ID_USUARIO == id).Select(u => u.ST_VIGENTE).SingleAsync());
        Assert.Equal(ResultadoLogin.CredencialesInvalidas, (await c.Autenticacion.ValidarAsync(id, password)).Resultado);
        var admin = await c.EjecutarSpXmlAsync("sp_desktop_login_admin", "C", ("ID_USUARIO", id), ("Password", password));
        Assert.NotEqual(1, Convert.ToInt32(admin.Tables[0].Rows[0]["EXITO"]));
    }

    [SkippableFact]
    public async Task No_se_puede_desactivar_a_si_mismo_ni_al_ultimo_administrador()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        const string id = "qa.web.tmp";
        await c.Usuarios.CrearAsync(new UsuarioAlta { IdUsuario = id, IdPerfil = 1, Nombre = "QA", Password = "Prueba#2026", ConfirmarPassword = "Prueba#2026" }, Actor);

        Assert.Equal(TipoResultado.Advertencia, (await c.Usuarios.CambiarEstadoAsync(id, false, actor: id)).Tipo);

        // Dentro de la transacción revertida se dejan inactivos los demás administradores.
        await c.EjecutarAsync("UPDATE dbo.SS_USUARIOS SET ST_VIGENTE = 'I' WHERE ID_PERFIL = 1 AND ID_USUARIO <> @id", new SqlParameter("@id", id));
        var r = await c.Usuarios.CambiarEstadoAsync(id, false, actor: "otro.admin");
        Assert.Equal(TipoResultado.Advertencia, r.Tipo);
        Assert.Contains("último", r.Mensaje);
    }

    [SkippableFact]
    public async Task Cambio_de_contrasena_propia_exige_la_actual()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        const string id = "qa.web.tmp";
        await c.Usuarios.CrearAsync(new UsuarioAlta { IdUsuario = id, IdPerfil = 1, Nombre = "QA", Password = "Prueba#2026", ConfirmarPassword = "Prueba#2026" }, Actor);

        Assert.Equal(TipoResultado.Error, (await c.Usuarios.CambiarPasswordPropiaAsync(id, "Equivocada1", "Nueva#2027")).Tipo);
        Assert.True((await c.Usuarios.CambiarPasswordPropiaAsync(id, "Prueba#2026", "Nueva#2027")).Exitoso);
        Assert.Equal(ResultadoLogin.Exito, (await c.Autenticacion.ValidarAsync(id, "Nueva#2027")).Resultado);
        Assert.DoesNotContain(c.Auditoria.Eventos, e => (e.Detalle ?? "").Contains("Nueva#2027") || (e.Detalle ?? "").Contains("Prueba#2026"));
    }

    [SkippableFact]
    public async Task Hash_unicode_coincide_con_HASHBYTES_nvarchar()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        const string password = "Contraseña_ñÁ1€";
        var sql = await c.EscalarAsync<string>("SELECT CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @p), 2)",
            new SqlParameter("@p", SqlDbType.NVarChar, 4000) { Value = password });
        Assert.Equal(sql, LegacyPasswordHasher.Hash(password));
    }

    [SkippableFact]
    public async Task Exportacion_de_marcajes_comparte_filtros_orden_y_normalizacion_sin_paginar()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();
        var inicial = await c.Marcajes.BuscarAsync(new MarcajeFiltro());
        var muestra = inicial.Elementos.FirstOrDefault();
        var desde = new DateOnly(2020, 1, 1);
        var hasta = muestra is null ? DateOnly.FromDateTime(DateTime.Today) : DateOnly.FromDateTime(muestra.Hora);
        var filtro = new MarcajeFiltro
        {
            Desde = desde, Hasta = hasta, Tipo = muestra?.Tipo,
            Trabajador = muestra?.IdTrabajador?.ToString(), Equipo = muestra?.Equipo,
            Pagina = 1, Tamano = 10,
        };
        var primera = await c.Marcajes.BuscarAsync(filtro);
        var exportacion = new MarcajeFiltro
        {
            Desde = hasta, Hasta = desde, Tipo = filtro.Tipo,
            Trabajador = filtro.Trabajador, Equipo = filtro.Equipo, Pagina = 999, Tamano = 10,
        };
        var filas = await c.Marcajes.ExportarAsync(exportacion);
        Assert.Equal(filtro.Desde, exportacion.Desde);
        Assert.Equal(filtro.Hasta, exportacion.Hasta);
        Assert.Equal(primera.Total, filas.Count);
        Assert.Equal(primera.Elementos, filas.Take(10).ToList());
        Assert.Equal(filas.OrderByDescending(m => m.Hora).ThenByDescending(m => m.Id), filas);
        if (muestra is not null)
        {
            Assert.NotEmpty(filas);
            Assert.All(filas, m =>
            {
                Assert.Equal(muestra.Tipo, m.Tipo);
                if (muestra.IdTrabajador is not null) Assert.Equal(muestra.IdTrabajador, m.IdTrabajador);
                if (!string.IsNullOrWhiteSpace(muestra.Equipo))
                    Assert.Contains(muestra.Equipo.Trim(), m.Equipo!, StringComparison.OrdinalIgnoreCase);
            });
        }
    }

    [SkippableFact]
    public async Task Consultas_de_lectura_funcionan_con_datos_reales()
    {
        RequiereBd();
        await using var c = await ContextoRevertido.CrearAsync();

        var empleados = await c.Empleados.BuscarAsync(new EmpleadoFiltro { Estado = "todos", Tamano = 10 });
        Assert.True(empleados.Total > 0);
        Assert.True(empleados.Elementos.Count <= 10);

        var filtro = new MarcajeFiltro();
        await c.Marcajes.BuscarAsync(filtro);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), filtro.Hasta);

        var usuarios = await c.Usuarios.BuscarAsync(new UsuarioFiltro { Estado = "todos" });
        Assert.True(usuarios.Total > 0);

        var perfiles = await c.Perfiles.ObtenerAsync();
        Assert.Contains(perfiles, p => p.Id == 1);
    }
}
