using System.Data;
using System.Xml.Linq;
using ChecadorWeb.Data;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using ChecadorWeb.Tests.Web;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChecadorWeb.Tests.Integracion;

/// <summary>
/// Contexto sobre la BD real dentro de una transacción que SIEMPRE se revierte: ningún cambio persiste.
/// </summary>
public sealed class ContextoRevertido : IAsyncDisposable
{
    public const string Actor = "qa.checadorweb";

    private readonly SqlConnection _conexion;
    private readonly IDbContextTransaction _tx;

    public ChecadorDbContext Db { get; }
    public AuditoriaFalsa Auditoria { get; } = new();
    public MonitorFijo<SeguridadOptions> Seguridad { get; } = new(new SeguridadOptions
    {
        PermisosPorPerfil = new() { ["1"] = ["*"] },
        NombresPerfil = new() { ["1"] = "Administrador" },
    });
    public MonitorFijo<HorariosOptions> Horarios { get; } = new(new HorariosOptions());

    private ContextoRevertido(SqlConnection conexion, ChecadorDbContext db, IDbContextTransaction tx)
    {
        _conexion = conexion;
        Db = db;
        _tx = tx;
    }

    public static async Task<ContextoRevertido> CrearAsync()
    {
        var cn = new SqlConnection(ChecadorWebFactory.CadenaConexion);
        await cn.OpenAsync();
        var opciones = new DbContextOptionsBuilder<ChecadorDbContext>()
            .UseSqlServer(cn, s => s.UseCompatibilityLevel(150))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        var db = new ChecadorDbContext(opciones);
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        return new ContextoRevertido(cn, db, tx);
    }

    public PermisoResolver Permisos => new(Seguridad);
    public EmpleadoService Empleados => new(Db, Auditoria, Horarios);
    public VinculoService Vinculos => new(Db, Auditoria);
    public UsuarioService Usuarios => new(Db, Auditoria, Perfiles, Permisos, Seguridad);
    public AutenticacionService Autenticacion => new(Db, Permisos, Seguridad);
    public PerfilCatalogo Perfiles => new(Db, new MemoryCache(new MemoryCacheOptions()), Seguridad, NullLogger<PerfilCatalogo>.Instance);
    public EstacionService Estaciones => new(Db, new MonitorFijo<HeartbeatOptions>(new HeartbeatOptions()));
    public MarcajeService Marcajes => new(Db);

    /// <summary>Ejecuta un procedimiento heredado con parámetro XML dentro de la misma transacción.</summary>
    public async Task<DataSet> EjecutarSpXmlAsync(string procedimiento, string accion, params (string Nombre, object? Valor)[] campos)
    {
        var raiz = new XElement("root", new XElement("accion", accion));
        foreach (var (nombre, valor) in campos) raiz.Add(new XElement(nombre, valor));

        await using var cmd = new SqlCommand(procedimiento, _conexion, (SqlTransaction)_tx.GetDbTransaction())
        {
            CommandType = CommandType.StoredProcedure,
        };
        cmd.Parameters.Add(new SqlParameter("@param", SqlDbType.NText) { Value = raiz.ToString(SaveOptions.DisableFormatting) });
        var ds = new DataSet();
        using var adaptador = new SqlDataAdapter(cmd);
        adaptador.Fill(ds);
        return ds;
    }

    public async Task<T> EscalarAsync<T>(string sql, params SqlParameter[] parametros)
    {
        await using var cmd = new SqlCommand(sql, _conexion, (SqlTransaction)_tx.GetDbTransaction());
        cmd.Parameters.AddRange(parametros);
        return (T)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task EjecutarAsync(string sql, params SqlParameter[] parametros)
    {
        await using var cmd = new SqlCommand(sql, _conexion, (SqlTransaction)_tx.GetDbTransaction());
        cmd.Parameters.AddRange(parametros);
        await cmd.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _tx.RollbackAsync();
        await _tx.DisposeAsync();
        await Db.DisposeAsync();
        await _conexion.DisposeAsync();
    }
}

public sealed class AuditoriaFalsa : IAuditoria
{
    public List<EventoAuditoria> Eventos { get; } = [];

    public Task RegistrarAsync(EventoAuditoria evento, CancellationToken ct = default)
    {
        Eventos.Add(evento);
        return Task.CompletedTask;
    }
}
