using ChecadorDesktop.Data;
using ChecadorDesktop.Models;
using Microsoft.Extensions.Configuration;

namespace ChecadorDesktop.Services;

public class SyncManager
{
    private readonly SQLiteRepository _repository;
    private readonly ApiClientService _apiClient;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private DateTime _ultimaPurga = DateTime.MinValue;
    private DateTime _ultimoHeartbeatEnviado = DateTime.MinValue;

    // Control de actualización periódica de horarios desde el servidor
    private DateTime _ultimaActualizacionHorarios = DateTime.MinValue;
    private readonly bool _scheduleRefreshEnabled;
    private readonly TimeSpan _cadenciaActualizacionHorarios;

    public event EventHandler<string>? OnSyncStatusChanged;

    /// <summary>
    /// Se dispara cuando el refresco periódico detecta que los horarios del trabajador
    /// cambiaron en el servidor. Permite que MainForm actualice el AlertScheduler en memoria.
    /// </summary>
    public event EventHandler<WorkerCacheItem>? OnHorariosActualizados;

    public SyncManager(SQLiteRepository repository, ApiClientService apiClient)
    {
        _repository = repository;
        _apiClient = apiClient;

        // Leer configuración de refresco de horarios desde appsettings.json
        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            _scheduleRefreshEnabled = bool.TryParse(config["ScheduleRefresh:Enabled"], out bool ena) ? ena : true;
            _cadenciaActualizacionHorarios = int.TryParse(config["ScheduleRefresh:CadenciaHoras"], out int horas) && horas > 0
                ? TimeSpan.FromHours(horas)
                : TimeSpan.FromHours(24);
        }
        catch
        {
            _scheduleRefreshEnabled = true;
            _cadenciaActualizacionHorarios = TimeSpan.FromHours(24);
        }
    }

    public void Iniciar(int intervaloSegundos = 15)
    {
        if (_isRunning) return;

        _isRunning = true;
        _cts = new CancellationTokenSource();

        Task.Run(async () => await CicloSincronizacionAsync(intervaloSegundos, _cts.Token));
        _repository.RegistrarLog("INFO", "SyncManager", "Servicio de sincronización en segundo plano iniciado.");
    }

    public void Detener()
    {
        if (!_isRunning) return;

        _cts?.Cancel();
        _isRunning = false;
        _repository.RegistrarLog("INFO", "SyncManager", "Servicio de sincronización detenido.");
    }

    private async Task CicloSincronizacionAsync(int intervaloSegundos, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                bool hayConexion = await _apiClient.CheckHealthAsync();
                int idTrabajadorActual = ObtenerTrabajadorActualParaHeartbeat();
                await EmitirHeartbeatSiCorrespondeAsync(idTrabajadorActual);

                if (hayConexion)
                {
                    OnSyncStatusChanged?.Invoke(this, "En línea. Procesando cola local...");
                    await ProcesarColaPendientesAsync();

                    // Purgar una vez cada 24 horas registros sincronizados > 30 días
                    if ((DateTime.Now - _ultimaPurga).TotalHours >= 24)
                    {
                        int eliminados = _repository.PurgarSincronizados(30);
                        if (eliminados > 0)
                        {
                            _repository.RegistrarLog("INFO", "SyncManager", $"Purga ejecutada: {eliminados} registros antiguos eliminados.");
                        }

                        _ultimaPurga = DateTime.Now;
                    }

                    // Refrescar horarios del trabajador una vez por cadencia configurada (default 24h)
                    if (_scheduleRefreshEnabled && (DateTime.Now - _ultimaActualizacionHorarios) >= _cadenciaActualizacionHorarios)
                    {
                        await RefrescarHorariosWorkerCacheAsync(idTrabajadorActual);
                    }
                }
                else
                {
                    OnSyncStatusChanged?.Invoke(this, "Sin conexión a la API. Modo fuera de línea activo.");
                }
            }
            catch (Exception ex)
            {
                _repository.RegistrarLog("ERROR", "SyncManager.Ciclo", ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervaloSegundos), token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private int ObtenerTrabajadorActualParaHeartbeat()
    {
        var sesion = _repository.ObtenerSesionActiva(Environment.MachineName);
        if (sesion != null && sesion.Activa && sesion.IdTrabajador > 0)
        {
            return sesion.IdTrabajador;
        }

        var vinculo = _repository.ObtenerVinculoLocal(Environment.MachineName);
        if (vinculo != null && vinculo.EstaVigente && vinculo.IdTrabajador > 0)
        {
            return vinculo.IdTrabajador;
        }

        return 0;
    }

    private async Task EmitirHeartbeatSiCorrespondeAsync(int idTrabajadorActual)
    {
        if (idTrabajadorActual <= 0) return;

        if ((DateTime.Now - _ultimoHeartbeatEnviado).TotalSeconds >= 60)
        {
            try
            {
                var res = await _apiClient.SendHeartbeatAsync(Environment.MachineName, idTrabajadorActual);
                if (res != null && res.Exito == 1)
                {
                    _ultimoHeartbeatEnviado = DateTime.Now;
                }
            }
            catch
            {
                // Falla silenciosa de heartbeat en modo offline
            }
        }
    }

    /// <summary>
    /// Consulta el endpoint GET /api/workers/{id}/schedule para obtener los horarios
    /// actualizados del trabajador y, si cambiaron, persiste en WORKER_CACHE y notifica
    /// al AlertScheduler mediante el evento OnHorariosActualizados.
    /// </summary>
    private async Task RefrescarHorariosWorkerCacheAsync(int idTrabajador)
    {
        if (idTrabajador <= 0)
        {
            _repository.RegistrarLog("WARN", "SyncManager.HorarioRefresh",
                "Sin trabajador activo vinculado. Actualización de horarios omitida.");
            return;
        }

        var ultimaPersistida = _repository.ObtenerUltimaActualizacionWorkerCachePorTrabajador(idTrabajador);
        if (ultimaPersistida.HasValue && (DateTime.Now - ultimaPersistida.Value) < _cadenciaActualizacionHorarios)
        {
            // Evita refrescos repetidos si la app reinicia dentro de la misma ventana diaria.
            _ultimaActualizacionHorarios = ultimaPersistida.Value;
            return;
        }

        var resp = await _apiClient.GetWorkerScheduleAsync(idTrabajador);

        if (resp == null || resp.Exito != 1 || resp.Data == null)
        {
            _repository.RegistrarLog("WARN", "SyncManager.HorarioRefresh",
                $"No se pudo actualizar horarios para trabajador={idTrabajador}. " +
                $"Motivo: {resp?.Mensaje ?? "Sin respuesta de API"}");
            // No actualizar _ultimaActualizacionHorarios: se reintentará en el próximo ciclo con conexión.
            return;
        }

        // Comparar con los valores actuales en caché para detectar cambios
        var cache = _repository.ObtenerWorkerCachePorTrabajador(idTrabajador);

        bool cambio = cache == null
            || cache.HoraEntrada != resp.Data.HoraEntrada
            || cache.HoraSalidaComida != resp.Data.HoraSalidaComida
            || cache.HoraEntradaComida != resp.Data.HoraEntradaComida
            || cache.HoraSalida != resp.Data.HoraSalida
            || cache.NombreCompleto != resp.Data.NombreCompleto;

        // Persistir siempre para mantener FechaActualizacion vigente
        var actualizado = new WorkerCacheItem
        {
            IdTrabajador = idTrabajador,
            Pin = cache?.Pin ?? 0,      // conservar el PIN existente
            NombreCompleto = resp.Data.NombreCompleto,
            HoraEntrada = resp.Data.HoraEntrada,
            HoraSalidaComida = resp.Data.HoraSalidaComida,
            HoraEntradaComida = resp.Data.HoraEntradaComida,
            HoraSalida = resp.Data.HoraSalida
        };
        _repository.GuardarWorkerCache(actualizado);

        string logMsg = cambio
            ? $"Horarios actualizados para trabajador={idTrabajador}. " +
              $"Entrada={resp.Data.HoraEntrada}, SalidaComida={resp.Data.HoraSalidaComida}, " +
              $"EntradaComida={resp.Data.HoraEntradaComida}, Salida={resp.Data.HoraSalida}"
            : $"Horarios verificados sin cambios para trabajador={idTrabajador}.";
        _repository.RegistrarLog("INFO", "SyncManager.HorarioRefresh", logMsg);

        // Notificar solo si los datos realmente cambiaron para que MainForm reaplique al AlertScheduler
        if (cambio)
        {
            OnHorariosActualizados?.Invoke(this, actualizado);
        }

        _ultimaActualizacionHorarios = DateTime.Now;
    }

    private async Task ProcesarColaPendientesAsync()
    {
        var pendientes = _repository.ObtenerPendientesSync(limite: 15);
        if (pendientes.Count == 0) return;

        // Marcar como 'Enviando' (EstadoSync = 1)
        foreach (var p in pendientes)
        {
            _repository.ActualizarEstadoSync(p.IdLocal, 1);
        }

        var listaDto = pendientes.Select(p => new MarcajeRequestDto
        {
            GuidRegistro = p.GuidRegistro,
            IdTrabajador = p.IDTrabajador,
            Pin = p.PIN,
            FechaHoraCaptura = p.FechaHoraCaptura,
            TipoRegistro = p.TipoRegistro,
            IpRegistro = p.IPRegistro,
            NbEquipo = p.NBEquipo,
            FotoBase64 = p.Foto.Length > 0 ? Convert.ToBase64String(p.Foto) : null
        }).ToList();

        var resultadoBatch = await _apiClient.RegistrarMarcajeOfflineBatchAsync(listaDto);

        if (resultadoBatch != null && resultadoBatch.Exito == 1)
        {
            // Sincronización exitosa total o procesada por el servidor
            foreach (var p in pendientes)
            {
                _repository.ActualizarEstadoSync(p.IdLocal, 2);
            }
            string detalle = resultadoBatch.Data != null
                ? $"Exitosos={resultadoBatch.Data.Exitosos}, Fallidos={resultadoBatch.Data.Fallidos}"
                : "Sin detalle";
            _repository.RegistrarLog("INFO", "SyncManager", $"Lote de {pendientes.Count} marcajes sincronizado con éxito. {detalle}");
            OnSyncStatusChanged?.Invoke(this, $"Sincronizados {pendientes.Count} registros correctamente.");
        }
        else
        {
            // Error en la transmisión: devolver a estado pendiente (0) para reintentar
            string error = resultadoBatch?.Mensaje ?? "Fallo en la comunicación con la API";
            foreach (var p in pendientes)
            {
                _repository.ActualizarEstadoSync(p.IdLocal, 0, error);
            }
            _repository.RegistrarLog("WARN", "SyncManager", $"Error al sincronizar lote: {error}");
        }
    }
}
