using ChecadorDesktop.Data;
using ChecadorDesktop.Models;

namespace ChecadorDesktop.Services;

public class AlertScheduler
{
    private readonly SQLiteRepository _repository;
    private readonly ApiClientService _apiClient;
    private readonly WebcamService _webcamService;

    private WorkerCacheItem? _trabajadorActual;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    // Control de pospuesto en memoria
    private DateTime _comidaSalidaPospuestaHasta = DateTime.MinValue;
    private DateTime _comidaEntradaPospuestaHasta = DateTime.MinValue;
    private readonly Dictionary<int, DateTime> _ultimosDisparosPorTipo = new();
    private readonly TimeSpan _cooldownDisparo = TimeSpan.FromMinutes(5);

    public event EventHandler<(int TipoRegistro, string HoraOficial)>? OnAlertaDisparada;

    public AlertScheduler(SQLiteRepository repository, ApiClientService apiClient, WebcamService webcamService)
    {
        _repository = repository;
        _apiClient = apiClient;
        _webcamService = webcamService;
    }

    public void AsignarTrabajador(WorkerCacheItem trabajador)
    {
        _trabajadorActual = trabajador;
        _comidaSalidaPospuestaHasta = DateTime.MinValue;
        _comidaEntradaPospuestaHasta = DateTime.MinValue;
    }

    public void Iniciar(int intervaloVerificacionSegundos = 30)
    {
        if (_isRunning) return;

        _isRunning = true;
        _cts = new CancellationTokenSource();

        Task.Run(async () => await CicloVerificacionAsync(intervaloVerificacionSegundos, _cts.Token));
        _repository.RegistrarLog("INFO", "AlertScheduler", "Programador de alertas iniciado.");
    }

    public void Detener()
    {
        if (!_isRunning) return;

        _cts?.Cancel();
        _isRunning = false;
        _repository.RegistrarLog("INFO", "AlertScheduler", "Programador de alertas detenido.");
    }

    public void PosponerAlerta(int tipoRegistro, int minutos = 10)
    {
        if (tipoRegistro == 2)
            _comidaSalidaPospuestaHasta = DateTime.Now.AddMinutes(minutos);
        else if (tipoRegistro == 3)
            _comidaEntradaPospuestaHasta = DateTime.Now.AddMinutes(minutos);

        _repository.RegistrarLog("INFO", "AlertScheduler", $"Alerta {tipoRegistro} pospuesta {minutos} minutos.");
    }

    private async Task CicloVerificacionAsync(int intervaloSegundos, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_trabajadorActual != null)
                {
                    EvaluarHorarios();
                }
            }
            catch (Exception ex)
            {
                _repository.RegistrarLog("ERROR", "AlertScheduler", ex.Message);
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

    private void EvaluarHorarios()
    {
        if (_trabajadorActual == null) return;

        DateTime ahora = DateTime.Now;
        if (!EsDiaLaboral(ahora))
            return;

        var ultimo = _repository.ObtenerUltimoRegistroDelDia(_trabajadorActual.IdTrabajador, ahora);
        int ultimoTipo = ultimo?.TipoRegistro ?? 0;

        // Regla de 5 minutos de espera entre checadas
        if (ultimo != null && (ahora - ultimo.FechaHoraCaptura).TotalMinutes < 5)
            return;

        // 1. Evaluar ENTRADA (TipoRegistro = 1)
        if (ultimoTipo == 0 && EsHoraDeDisparo(_trabajadorActual.HoraEntrada, ahora) && PuedeDisparar(1, ahora))
        {
            MarcarDisparo(1, ahora);
            OnAlertaDisparada?.Invoke(this, (1, _trabajadorActual.HoraEntrada ?? "Oficial"));
            return;
        }

        // 2. Evaluar SALIDA A COMER (TipoRegistro = 2)
        if (ultimoTipo == 1 && ahora >= _comidaSalidaPospuestaHasta && EsHoraDeDisparo(_trabajadorActual.HoraSalidaComida, ahora) && PuedeDisparar(2, ahora))
        {
            MarcarDisparo(2, ahora);
            OnAlertaDisparada?.Invoke(this, (2, _trabajadorActual.HoraSalidaComida ?? "Oficial"));
            return;
        }

        // 3. Evaluar REGRESO DE COMIDA (TipoRegistro = 3)
        if (ultimoTipo == 2 && ahora >= _comidaEntradaPospuestaHasta && EsHoraDeDisparo(_trabajadorActual.HoraEntradaComida, ahora) && PuedeDisparar(3, ahora))
        {
            MarcarDisparo(3, ahora);
            OnAlertaDisparada?.Invoke(this, (3, _trabajadorActual.HoraEntradaComida ?? "Oficial"));
            return;
        }

        // 4. Evaluar SALIDA FINAL (TipoRegistro = 4)
        if (ultimoTipo == 3 && EsHoraDeDisparo(_trabajadorActual.HoraSalida, ahora) && PuedeDisparar(4, ahora))
        {
            MarcarDisparo(4, ahora);
            OnAlertaDisparada?.Invoke(this, (4, _trabajadorActual.HoraSalida ?? "Oficial"));
        }
    }

    private bool PuedeDisparar(int tipoRegistro, DateTime ahora)
    {
        if (!_ultimosDisparosPorTipo.TryGetValue(tipoRegistro, out var ultimaEjecucion))
        {
            return true;
        }

        return (ahora - ultimaEjecucion) >= _cooldownDisparo;
    }

    private void MarcarDisparo(int tipoRegistro, DateTime ahora)
    {
        _ultimosDisparosPorTipo[tipoRegistro] = ahora;
    }

    private static bool EsHoraDeDisparo(string? horaCadena, DateTime ahora)
    {
        if (string.IsNullOrWhiteSpace(horaCadena)) return false;

        if (TimeSpan.TryParse(horaCadena, out var horaEsperada))
        {
            TimeSpan horaActual = ahora.TimeOfDay;
            // Se dispara si ya llegó la hora oficial (con una tolerancia hacia adelante)
            return horaActual >= horaEsperada;
        }

        return false;
    }

    public static bool EsDiaLaboral(DateTime fecha)
    {
        return fecha.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    }
}
