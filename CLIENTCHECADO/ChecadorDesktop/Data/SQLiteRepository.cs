using System.Data;
using System.Data.SQLite;
using System.Globalization;
using ChecadorDesktop.Models;

namespace ChecadorDesktop.Data;

public class SQLiteRepository
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public SQLiteRepository(string? dbPath = null)
    {
        _dbPath = string.IsNullOrWhiteSpace(dbPath)
            ? Path.Combine(AppContext.BaseDirectory, "ChecadorLocal.db")
            : dbPath;

        _connectionString = $"Data Source={_dbPath};Version=3;";
        InicializarBaseDatos();
    }

    private void InicializarBaseDatos()
    {
        var dbDirectory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrWhiteSpace(dbDirectory) && !Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS COLA_REGISTROS (
    IdLocal INTEGER PRIMARY KEY AUTOINCREMENT,
    GuidRegistro TEXT NOT NULL,
    IDTrabajador INTEGER NOT NULL,
    PIN INTEGER NOT NULL,
    FechaHoraCaptura TEXT NOT NULL,
    TipoRegistro INTEGER NOT NULL,
    IPRegistro TEXT NOT NULL,
    NBEquipo TEXT NOT NULL,
    Foto BLOB NOT NULL,
    EsFotoReal INTEGER NOT NULL DEFAULT 0,
    MotivoFallback TEXT NULL,
    CameraStatus TEXT NULL,
    CameraDeviceId TEXT NULL,
    HashLocal TEXT NOT NULL,
    EstadoSync INTEGER NOT NULL,
    IntentosSync INTEGER NOT NULL DEFAULT 0,
    UltimoError TEXT NULL,
    FechaHoraSincronizacion TEXT NULL
);

CREATE INDEX IF NOT EXISTS IDX_COLA_SYNC ON COLA_REGISTROS(EstadoSync, FechaHoraCaptura);
CREATE INDEX IF NOT EXISTS IDX_COLA_TRABAJADOR_FECHA ON COLA_REGISTROS(IDTrabajador, FechaHoraCaptura);

CREATE TABLE IF NOT EXISTS WORKER_CACHE (
    IdTrabajador INTEGER NOT NULL,
    Pin INTEGER NOT NULL,
    NombreCompleto TEXT NOT NULL,
    HoraEntrada TEXT NULL,
    HoraSalidaComida TEXT NULL,
    HoraEntradaComida TEXT NULL,
    HoraSalida TEXT NULL,
    FechaActualizacion TEXT NOT NULL,
    PRIMARY KEY (IdTrabajador, Pin)
);

CREATE TABLE IF NOT EXISTS [ESTACION_VINCULO_LOCAL] (
    [NBEquipo]           TEXT PRIMARY KEY,
    [IDTrabajador]       INTEGER NOT NULL,
    [NombreTrabajador]   TEXT NOT NULL,
    [FechaVinculacion]   TEXT NOT NULL,
    [VigenteHasta]       TEXT NOT NULL,
    [Activo]             INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS WORKER_SESSION (
    NBEquipo TEXT PRIMARY KEY,
    IdTrabajador INTEGER NOT NULL,
    Pin INTEGER NOT NULL,
    NombreCompleto TEXT NOT NULL,
    HoraEntrada TEXT NULL,
    HoraSalidaComida TEXT NULL,
    HoraEntradaComida TEXT NULL,
    HoraSalida TEXT NULL,
    FechaInicio TEXT NOT NULL,
    FechaUltimaActividad TEXT NOT NULL,
    TipoRegistroUltimo INTEGER NOT NULL,
    Activa INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX IF NOT EXISTS IDX_WORKER_SESSION_ACTIVA ON WORKER_SESSION(Activa, FechaUltimaActividad);

CREATE TABLE IF NOT EXISTS BITACORA_LOCAL (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FechaHora TEXT NOT NULL,
    Nivel TEXT NOT NULL,
    Origen TEXT NOT NULL,
    Mensaje TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IDX_BITACORA_FECHA ON BITACORA_LOCAL(FechaHora);
";
        command.ExecuteNonQuery();

        AsegurarColumnasCameraMetadata(connection);
    }

    private static void AsegurarColumnasCameraMetadata(SQLiteConnection connection)
    {
        using var alter1 = connection.CreateCommand();
        alter1.CommandText = "ALTER TABLE COLA_REGISTROS ADD COLUMN EsFotoReal INTEGER NOT NULL DEFAULT 0;";
        TryExecuteAlter(alter1);

        using var alter2 = connection.CreateCommand();
        alter2.CommandText = "ALTER TABLE COLA_REGISTROS ADD COLUMN MotivoFallback TEXT NULL;";
        TryExecuteAlter(alter2);

        using var alter3 = connection.CreateCommand();
        alter3.CommandText = "ALTER TABLE COLA_REGISTROS ADD COLUMN CameraStatus TEXT NULL;";
        TryExecuteAlter(alter3);

        using var alter4 = connection.CreateCommand();
        alter4.CommandText = "ALTER TABLE COLA_REGISTROS ADD COLUMN CameraDeviceId TEXT NULL;";
        TryExecuteAlter(alter4);
    }

    private static void TryExecuteAlter(SQLiteCommand command)
    {
        try
        {
            command.ExecuteNonQuery();
        }
        catch (SQLiteException ex) when (ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
            // Columna ya existente en base previa.
        }
    }

    public long InsertarRegistroLocal(RegistroAsistenciaLocal reg)
    {
        ArgumentNullException.ThrowIfNull(reg);

        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO COLA_REGISTROS
(GuidRegistro, IDTrabajador, PIN, FechaHoraCaptura, TipoRegistro, IPRegistro, NBEquipo, Foto, EsFotoReal, MotivoFallback, CameraStatus, CameraDeviceId, HashLocal, EstadoSync, IntentosSync, UltimoError, FechaHoraSincronizacion)
VALUES
(@GuidRegistro, @IDTrabajador, @PIN, @FechaHoraCaptura, @TipoRegistro, @IPRegistro, @NBEquipo, @Foto, @EsFotoReal, @MotivoFallback, @CameraStatus, @CameraDeviceId, @HashLocal, @EstadoSync, @IntentosSync, @UltimoError, @FechaHoraSincronizacion);
SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@GuidRegistro", reg.GuidRegistro);
        command.Parameters.AddWithValue("@IDTrabajador", reg.IDTrabajador);
        command.Parameters.AddWithValue("@PIN", reg.PIN);
        command.Parameters.AddWithValue("@FechaHoraCaptura", FormatoFecha(reg.FechaHoraCaptura));
        command.Parameters.AddWithValue("@TipoRegistro", reg.TipoRegistro);
        command.Parameters.AddWithValue("@IPRegistro", reg.IPRegistro);
        command.Parameters.AddWithValue("@NBEquipo", reg.NBEquipo);
        command.Parameters.Add("@Foto", DbType.Binary, reg.Foto.Length).Value = reg.Foto;
        command.Parameters.AddWithValue("@EsFotoReal", reg.EsFotoReal ? 1 : 0);
        command.Parameters.AddWithValue("@MotivoFallback", (object?)reg.MotivoFallback ?? DBNull.Value);
        command.Parameters.AddWithValue("@CameraStatus", (object?)reg.CameraStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("@CameraDeviceId", (object?)reg.CameraDeviceId ?? DBNull.Value);
        command.Parameters.AddWithValue("@HashLocal", reg.HashLocal);
        command.Parameters.AddWithValue("@EstadoSync", reg.EstadoSync);
        command.Parameters.AddWithValue("@IntentosSync", reg.IntentosSync);
        command.Parameters.AddWithValue("@UltimoError", (object?)reg.UltimoError ?? DBNull.Value);
        command.Parameters.AddWithValue("@FechaHoraSincronizacion", reg.FechaHoraSincronizacion is null
            ? DBNull.Value
            : FormatoFecha(reg.FechaHoraSincronizacion.Value));

        var result = command.ExecuteScalar();
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public List<RegistroAsistenciaLocal> ObtenerPendientesSync(int limite = 10)
    {
        if (limite <= 0)
        {
            limite = 10;
        }

        var registros = new List<RegistroAsistenciaLocal>();

        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT IdLocal, GuidRegistro, IDTrabajador, PIN, FechaHoraCaptura, TipoRegistro, IPRegistro, NBEquipo, Foto, EsFotoReal, MotivoFallback, CameraStatus, CameraDeviceId, HashLocal, EstadoSync, IntentosSync, UltimoError, FechaHoraSincronizacion
FROM COLA_REGISTROS
WHERE EstadoSync IN (0, 3)
ORDER BY FechaHoraCaptura ASC, IdLocal ASC
LIMIT @Limite;";
        command.Parameters.AddWithValue("@Limite", limite);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            registros.Add(MapRegistro(reader));
        }

        return registros;
    }

    public void ActualizarEstadoSync(long idLocal, int nuevoEstado, string? error = null)
    {
        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE COLA_REGISTROS
SET
    EstadoSync = @EstadoSync,
    UltimoError = @UltimoError,
    IntentosSync = CASE WHEN @EstadoSync = 3 THEN IntentosSync + 1 ELSE IntentosSync END,
    FechaHoraSincronizacion = CASE WHEN @EstadoSync = 2 THEN @FechaHoraSincronizacion ELSE FechaHoraSincronizacion END
WHERE IdLocal = @IdLocal;";

        command.Parameters.AddWithValue("@EstadoSync", nuevoEstado);
        command.Parameters.AddWithValue("@UltimoError", string.IsNullOrWhiteSpace(error) ? DBNull.Value : error);
        command.Parameters.AddWithValue("@FechaHoraSincronizacion", nuevoEstado == 2
            ? FormatoFecha(DateTime.Now)
            : DBNull.Value);
        command.Parameters.AddWithValue("@IdLocal", idLocal);
        command.ExecuteNonQuery();
    }

    public RegistroAsistenciaLocal? ObtenerUltimoRegistroDelDia(int idTrabajador, DateTime fecha)
    {
        var inicio = fecha.Date;
        var fin = inicio.AddDays(1);

        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT IdLocal, GuidRegistro, IDTrabajador, PIN, FechaHoraCaptura, TipoRegistro, IPRegistro, NBEquipo, Foto, EsFotoReal, MotivoFallback, CameraStatus, CameraDeviceId, HashLocal, EstadoSync, IntentosSync, UltimoError, FechaHoraSincronizacion
FROM COLA_REGISTROS
WHERE IDTrabajador = @IdTrabajador
  AND FechaHoraCaptura >= @Inicio
  AND FechaHoraCaptura < @Fin
ORDER BY FechaHoraCaptura DESC, IdLocal DESC
LIMIT 1;";

        command.Parameters.AddWithValue("@IdTrabajador", idTrabajador);
        command.Parameters.AddWithValue("@Inicio", FormatoFecha(inicio));
        command.Parameters.AddWithValue("@Fin", FormatoFecha(fin));

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return MapRegistro(reader);
        }

        return null;
    }

    public void GuardarWorkerCache(WorkerCacheItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO WORKER_CACHE
(IdTrabajador, Pin, NombreCompleto, HoraEntrada, HoraSalidaComida, HoraEntradaComida, HoraSalida, FechaActualizacion)
VALUES
(@IdTrabajador, @Pin, @NombreCompleto, @HoraEntrada, @HoraSalidaComida, @HoraEntradaComida, @HoraSalida, @FechaActualizacion)
ON CONFLICT(IdTrabajador, Pin)
DO UPDATE SET
    NombreCompleto = excluded.NombreCompleto,
    HoraEntrada = excluded.HoraEntrada,
    HoraSalidaComida = excluded.HoraSalidaComida,
    HoraEntradaComida = excluded.HoraEntradaComida,
    HoraSalida = excluded.HoraSalida,
    FechaActualizacion = excluded.FechaActualizacion;";

        command.Parameters.AddWithValue("@IdTrabajador", item.IdTrabajador);
        command.Parameters.AddWithValue("@Pin", item.Pin);
        command.Parameters.AddWithValue("@NombreCompleto", item.NombreCompleto);
        command.Parameters.AddWithValue("@HoraEntrada", (object?)item.HoraEntrada ?? DBNull.Value);
        command.Parameters.AddWithValue("@HoraSalidaComida", (object?)item.HoraSalidaComida ?? DBNull.Value);
        command.Parameters.AddWithValue("@HoraEntradaComida", (object?)item.HoraEntradaComida ?? DBNull.Value);
        command.Parameters.AddWithValue("@HoraSalida", (object?)item.HoraSalida ?? DBNull.Value);
        command.Parameters.AddWithValue("@FechaActualizacion", FormatoFecha(DateTime.Now));

        command.ExecuteNonQuery();
    }

    public WorkerCacheItem? ObtenerWorkerCache(int idTrabajador, int pin)
    {
        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT IdTrabajador, Pin, NombreCompleto, HoraEntrada, HoraSalidaComida, HoraEntradaComida, HoraSalida
FROM WORKER_CACHE
WHERE IdTrabajador = @IdTrabajador
  AND Pin = @Pin
LIMIT 1;";

        command.Parameters.AddWithValue("@IdTrabajador", idTrabajador);
        command.Parameters.AddWithValue("@Pin", pin);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new WorkerCacheItem
        {
            IdTrabajador = reader.GetInt32(0),
            Pin = reader.GetInt32(1),
            NombreCompleto = reader.GetString(2),
            HoraEntrada = reader.IsDBNull(3) ? null : reader.GetString(3),
            HoraSalidaComida = reader.IsDBNull(4) ? null : reader.GetString(4),
            HoraEntradaComida = reader.IsDBNull(5) ? null : reader.GetString(5),
            HoraSalida = reader.IsDBNull(6) ? null : reader.GetString(6)
        };
    }

    public WorkerCacheItem? ObtenerWorkerCachePorTrabajador(int idTrabajador)
    {
        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT IdTrabajador, Pin, NombreCompleto, HoraEntrada, HoraSalidaComida, HoraEntradaComida, HoraSalida
FROM WORKER_CACHE
WHERE IdTrabajador = @IdTrabajador
ORDER BY FechaActualizacion DESC
LIMIT 1;";

        command.Parameters.AddWithValue("@IdTrabajador", idTrabajador);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new WorkerCacheItem
        {
            IdTrabajador = reader.GetInt32(0),
            Pin = reader.GetInt32(1),
            NombreCompleto = reader.GetString(2),
            HoraEntrada = reader.IsDBNull(3) ? null : reader.GetString(3),
            HoraSalidaComida = reader.IsDBNull(4) ? null : reader.GetString(4),
            HoraEntradaComida = reader.IsDBNull(5) ? null : reader.GetString(5),
            HoraSalida = reader.IsDBNull(6) ? null : reader.GetString(6)
        };
    }

    public DateTime? ObtenerUltimaActualizacionWorkerCachePorTrabajador(int idTrabajador)
    {
        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT FechaActualizacion
FROM WORKER_CACHE
WHERE IdTrabajador = @IdTrabajador
ORDER BY FechaActualizacion DESC
LIMIT 1;";

        command.Parameters.AddWithValue("@IdTrabajador", idTrabajador);

        var raw = command.ExecuteScalar();
        if (raw == null || raw == DBNull.Value)
        {
            return null;
        }

        string valor = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
        if (DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedIso))
        {
            return parsedIso;
        }

        if (DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedLocal))
        {
            return parsedLocal;
        }

        return null;
    }

    /// <summary>
    /// Guarda o actualiza el vínculo de la estación de trabajo en la base de datos local SQLite.
    /// </summary>
    public void GuardarVinculoLocal(EstacionVinculoLocal vinculo)
    {
        using var conn = new SQLiteConnection(_connectionString);
        conn.Open();

        const string query = @"
        INSERT INTO ESTACION_VINCULO_LOCAL (NBEquipo, IDTrabajador, NombreTrabajador, FechaVinculacion, VigenteHasta, Activo)
        VALUES (@nb, @idTrab, @nombre, @fechaVinc, @vigHasta, @activo)
        ON CONFLICT(NBEquipo) DO UPDATE SET
            IDTrabajador = excluded.IDTrabajador,
            NombreTrabajador = excluded.NombreTrabajador,
            FechaVinculacion = excluded.FechaVinculacion,
            VigenteHasta = excluded.VigenteHasta,
            Activo = excluded.Activo;";

        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@nb", vinculo.NbEquipo);
        cmd.Parameters.AddWithValue("@idTrab", vinculo.IdTrabajador);
        cmd.Parameters.AddWithValue("@nombre", vinculo.NombreTrabajador);
        cmd.Parameters.AddWithValue("@fechaVinc", vinculo.FechaVinculacion.ToString("o"));
        cmd.Parameters.AddWithValue("@vigHasta", vinculo.VigenteHasta.ToString("o"));
        cmd.Parameters.AddWithValue("@activo", vinculo.Activo ? 1 : 0);

        cmd.ExecuteNonQuery();
        RegistrarLog("INFO", "SQLiteRepository", $"Vínculo local guardado para equipo '{vinculo.NbEquipo}' con trabajador {vinculo.IdTrabajador}. Activo={vinculo.Activo}.");
    }

    /// <summary>
    /// Obtiene el registro de vinculación local de la máquina actual.
    /// </summary>
    public EstacionVinculoLocal? ObtenerVinculoLocal(string nbEquipo)
    {
        using var conn = new SQLiteConnection(_connectionString);
        conn.Open();

        const string query = @"
        SELECT NBEquipo, IDTrabajador, NombreTrabajador, FechaVinculacion, VigenteHasta, Activo
        FROM ESTACION_VINCULO_LOCAL
        WHERE NBEquipo = @nb LIMIT 1;";

        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@nb", nbEquipo);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new EstacionVinculoLocal
        {
            NbEquipo = reader["NBEquipo"].ToString() ?? nbEquipo,
            IdTrabajador = Convert.ToInt32(reader["IDTrabajador"]),
            NombreTrabajador = reader["NombreTrabajador"].ToString() ?? string.Empty,
            FechaVinculacion = DateTime.Parse(reader["FechaVinculacion"].ToString()!),
            VigenteHasta = DateTime.Parse(reader["VigenteHasta"].ToString()!),
            Activo = Convert.ToInt32(reader["Activo"]) == 1
        };
    }

    /// <summary>
    /// Marca como inactivo el vínculo de la estación local tras una desvinculación administrativa.
    /// </summary>
    public void DesvincularLocal(string nbEquipo)
    {
        using var conn = new SQLiteConnection(_connectionString);
        conn.Open();

        const string query = "UPDATE ESTACION_VINCULO_LOCAL SET Activo = 0 WHERE NBEquipo = @nb;";
        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@nb", nbEquipo);
        cmd.ExecuteNonQuery();

        RegistrarLog("INFO", "SQLiteRepository", $"Vínculo local desactivado para equipo '{nbEquipo}'.");
    }

    public int PurgarSincronizados(int dias = 30)
    {
        if (dias <= 0)
        {
            dias = 30;
        }

        var fechaCorte = DateTime.Now.AddDays(-dias);

        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
DELETE FROM COLA_REGISTROS
WHERE EstadoSync = 2
  AND FechaHoraSincronizacion IS NOT NULL
  AND FechaHoraSincronizacion < @FechaCorte;";
        command.Parameters.AddWithValue("@FechaCorte", FormatoFecha(fechaCorte));

        return command.ExecuteNonQuery();
    }

    public void RegistrarLog(string nivel, string origen, string mensaje)
    {
        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO BITACORA_LOCAL (FechaHora, Nivel, Origen, Mensaje)
VALUES (@FechaHora, @Nivel, @Origen, @Mensaje);";

        command.Parameters.AddWithValue("@FechaHora", FormatoFecha(DateTime.Now));
        command.Parameters.AddWithValue("@Nivel", nivel);
        command.Parameters.AddWithValue("@Origen", origen);
        command.Parameters.AddWithValue("@Mensaje", mensaje);
        command.ExecuteNonQuery();
    }

    public void GuardarSesionActiva(WorkerSessionLocal sesion)
    {
        ArgumentNullException.ThrowIfNull(sesion);

        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO WORKER_SESSION
(NBEquipo, IdTrabajador, Pin, NombreCompleto, HoraEntrada, HoraSalidaComida, HoraEntradaComida, HoraSalida, FechaInicio, FechaUltimaActividad, TipoRegistroUltimo, Activa)
VALUES
(@NBEquipo, @IdTrabajador, @Pin, @NombreCompleto, @HoraEntrada, @HoraSalidaComida, @HoraEntradaComida, @HoraSalida, @FechaInicio, @FechaUltimaActividad, @TipoRegistroUltimo, @Activa)
ON CONFLICT(NBEquipo)
DO UPDATE SET
    IdTrabajador = excluded.IdTrabajador,
    Pin = excluded.Pin,
    NombreCompleto = excluded.NombreCompleto,
    HoraEntrada = excluded.HoraEntrada,
    HoraSalidaComida = excluded.HoraSalidaComida,
    HoraEntradaComida = excluded.HoraEntradaComida,
    HoraSalida = excluded.HoraSalida,
    FechaInicio = excluded.FechaInicio,
    FechaUltimaActividad = excluded.FechaUltimaActividad,
    TipoRegistroUltimo = excluded.TipoRegistroUltimo,
    Activa = excluded.Activa;";

        command.Parameters.AddWithValue("@NBEquipo", sesion.NbEquipo);
        command.Parameters.AddWithValue("@IdTrabajador", sesion.IdTrabajador);
        command.Parameters.AddWithValue("@Pin", sesion.Pin);
        command.Parameters.AddWithValue("@NombreCompleto", sesion.NombreCompleto);
        command.Parameters.AddWithValue("@HoraEntrada", (object?)sesion.HoraEntrada ?? DBNull.Value);
        command.Parameters.AddWithValue("@HoraSalidaComida", (object?)sesion.HoraSalidaComida ?? DBNull.Value);
        command.Parameters.AddWithValue("@HoraEntradaComida", (object?)sesion.HoraEntradaComida ?? DBNull.Value);
        command.Parameters.AddWithValue("@HoraSalida", (object?)sesion.HoraSalida ?? DBNull.Value);
        command.Parameters.AddWithValue("@FechaInicio", FormatoFecha(sesion.FechaInicio));
        command.Parameters.AddWithValue("@FechaUltimaActividad", FormatoFecha(sesion.FechaUltimaActividad));
        command.Parameters.AddWithValue("@TipoRegistroUltimo", sesion.TipoRegistroUltimo);
        command.Parameters.AddWithValue("@Activa", sesion.Activa ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public WorkerSessionLocal? ObtenerSesionActiva(string nbEquipo)
    {
        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT NBEquipo, IdTrabajador, Pin, NombreCompleto, HoraEntrada, HoraSalidaComida, HoraEntradaComida, HoraSalida, FechaInicio, FechaUltimaActividad, TipoRegistroUltimo, Activa
FROM WORKER_SESSION
WHERE NBEquipo = @NBEquipo
  AND Activa = 1
LIMIT 1;";
        command.Parameters.AddWithValue("@NBEquipo", nbEquipo);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new WorkerSessionLocal
        {
            NbEquipo = reader.GetString(0),
            IdTrabajador = reader.GetInt32(1),
            Pin = reader.GetInt32(2),
            NombreCompleto = reader.GetString(3),
            HoraEntrada = reader.IsDBNull(4) ? null : reader.GetString(4),
            HoraSalidaComida = reader.IsDBNull(5) ? null : reader.GetString(5),
            HoraEntradaComida = reader.IsDBNull(6) ? null : reader.GetString(6),
            HoraSalida = reader.IsDBNull(7) ? null : reader.GetString(7),
            FechaInicio = ParseFecha(reader.GetString(8)),
            FechaUltimaActividad = ParseFecha(reader.GetString(9)),
            TipoRegistroUltimo = reader.GetInt32(10),
            Activa = reader.GetInt32(11) == 1
        };
    }

    public void ActualizarSesionActividad(string nbEquipo, int tipoRegistroUltimo)
    {
        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE WORKER_SESSION
SET FechaUltimaActividad = @FechaUltimaActividad,
    TipoRegistroUltimo = @TipoRegistroUltimo
WHERE NBEquipo = @NBEquipo
  AND Activa = 1;";

        command.Parameters.AddWithValue("@FechaUltimaActividad", FormatoFecha(DateTime.Now));
        command.Parameters.AddWithValue("@TipoRegistroUltimo", tipoRegistroUltimo);
        command.Parameters.AddWithValue("@NBEquipo", nbEquipo);
        command.ExecuteNonQuery();
    }

    public void CerrarSesionActiva(string nbEquipo)
    {
        using var connection = CrearConexionAbierta();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE WORKER_SESSION
SET Activa = 0,
    FechaUltimaActividad = @FechaUltimaActividad
WHERE NBEquipo = @NBEquipo
  AND Activa = 1;";

        command.Parameters.AddWithValue("@FechaUltimaActividad", FormatoFecha(DateTime.Now));
        command.Parameters.AddWithValue("@NBEquipo", nbEquipo);
        command.ExecuteNonQuery();
    }

    public void InvalidarSesionActiva(string nbEquipo, string motivo)
    {
        CerrarSesionActiva(nbEquipo);
        RegistrarLog("WARN", "Sesion", $"Sesión local invalidada. Equipo={nbEquipo}; Motivo={motivo}");
    }

    private SQLiteConnection CrearConexionAbierta()
    {
        var connection = new SQLiteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static string FormatoFecha(DateTime fecha)
    {
        return fecha.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    private static DateTime ParseFecha(string fecha)
    {
        return DateTime.Parse(fecha, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
    }

    private static RegistroAsistenciaLocal MapRegistro(SQLiteDataReader reader)
    {
        return new RegistroAsistenciaLocal
        {
            IdLocal = reader.GetInt64(0),
            GuidRegistro = reader.GetString(1),
            IDTrabajador = reader.GetInt32(2),
            PIN = reader.GetInt32(3),
            FechaHoraCaptura = ParseFecha(reader.GetString(4)),
            TipoRegistro = reader.GetInt32(5),
            IPRegistro = reader.GetString(6),
            NBEquipo = reader.GetString(7),
            Foto = reader.IsDBNull(8) ? Array.Empty<byte>() : (byte[])reader[8],
            EsFotoReal = !reader.IsDBNull(9) && reader.GetInt32(9) == 1,
            MotivoFallback = reader.IsDBNull(10) ? null : reader.GetString(10),
            CameraStatus = reader.IsDBNull(11) ? null : reader.GetString(11),
            CameraDeviceId = reader.IsDBNull(12) ? null : reader.GetString(12),
            HashLocal = reader.GetString(13),
            EstadoSync = reader.GetInt32(14),
            IntentosSync = reader.GetInt32(15),
            UltimoError = reader.IsDBNull(16) ? null : reader.GetString(16),
            FechaHoraSincronizacion = reader.IsDBNull(17) ? null : ParseFecha(reader.GetString(17))
        };
    }
}
