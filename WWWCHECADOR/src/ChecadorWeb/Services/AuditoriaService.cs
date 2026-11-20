using System.Text.Encodings.Web;
using System.Text.Json;
using ChecadorWeb.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChecadorWeb.Services;

public sealed class AuditoriaOptions
{
    public const string Seccion = "Auditoria";

    /// <summary>Carpeta (relativa al ContentRoot o absoluta) para la bitácora JSONL.</summary>
    public string CarpetaArchivo { get; set; } = "App_Data/auditoria";

    /// <summary>
    /// Escribe además en dbo.SS_AUDITORIA. Activar solo después de crear la tabla con
    /// docs/sql/propuestas/02_SS_AUDITORIA.sql (cambio de esquema que requiere autorización).
    /// </summary>
    public bool RegistrarEnBaseDatos { get; set; }
}

public sealed record EventoAuditoria(
    string Accion,
    string Entidad,
    string? Clave,
    bool Exitoso,
    string? Detalle = null);

public interface IAuditoria
{
    /// <summary>
    /// Registra una operación con el usuario autenticado. El detalle nunca debe incluir contraseñas ni PIN.
    /// </summary>
    Task RegistrarAsync(EventoAuditoria evento, CancellationToken ct = default);
}

public sealed class AuditoriaService(
    IHttpContextAccessor http,
    ChecadorDbContext db,
    IOptionsMonitor<AuditoriaOptions> opciones,
    IWebHostEnvironment env,
    ILogger<AuditoriaService> logger) : IAuditoria
{
    private static readonly SemaphoreSlim Candado = new(1, 1);
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public async Task RegistrarAsync(EventoAuditoria evento, CancellationToken ct = default)
    {
        var ctx = http.HttpContext;
        var usuario = ctx?.User.Identity?.IsAuthenticated == true
            ? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : null;
        var ip = ctx?.Connection.RemoteIpAddress?.ToString();
        var fecha = DateTimeOffset.Now;

        logger.LogInformation("AUDITORIA {Accion} {Entidad} {Clave} exitoso={Exitoso} usuario={Usuario} ip={Ip}",
            evento.Accion, evento.Entidad, evento.Clave, evento.Exitoso, usuario ?? "(anónimo)", ip);

        var registro = new
        {
            fecha,
            usuario,
            ip,
            evento.Accion,
            evento.Entidad,
            evento.Clave,
            evento.Exitoso,
            evento.Detalle,
            traza = ctx?.TraceIdentifier,
        };

        try
        {
            var carpeta = Path.IsPathRooted(opciones.CurrentValue.CarpetaArchivo)
                ? opciones.CurrentValue.CarpetaArchivo
                : Path.Combine(env.ContentRootPath, opciones.CurrentValue.CarpetaArchivo);
            Directory.CreateDirectory(carpeta);
            var archivo = Path.Combine(carpeta, $"auditoria-{fecha:yyyyMMdd}.jsonl");
            var linea = JsonSerializer.Serialize(registro, Json) + Environment.NewLine;

            await Candado.WaitAsync(ct);
            try { await File.AppendAllTextAsync(archivo, linea, ct); }
            finally { Candado.Release(); }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudo escribir la bitácora de auditoría en archivo.");
        }

        if (!opciones.CurrentValue.RegistrarEnBaseDatos) return;

        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO dbo.SS_AUDITORIA (FE_EVENTO, ID_USUARIO, NB_ACCION, NB_ENTIDAD, TX_CLAVE, ST_EXITOSO, TX_DETALLE, TX_IP, TX_TRAZA)
VALUES (SYSDATETIMEOFFSET(), {usuario}, {evento.Accion}, {evento.Entidad}, {evento.Clave}, {evento.Exitoso}, {evento.Detalle}, {ip}, {ctx?.TraceIdentifier})", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudo escribir la auditoría en dbo.SS_AUDITORIA.");
        }
    }
}
