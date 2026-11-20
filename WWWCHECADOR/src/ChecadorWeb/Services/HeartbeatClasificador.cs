namespace ChecadorWeb.Services;

public sealed class HeartbeatOptions
{
    public const string Seccion = "Heartbeat";

    /// <summary>Minutos sin heartbeat a partir de los cuales se muestra aviso (la vista existente usa 3).</summary>
    public int MinutosAviso { get; set; } = 3;

    /// <summary>Minutos sin heartbeat a partir de los cuales se considera sin comunicación (la vista existente usa 15).</summary>
    public int MinutosDesconexion { get; set; } = 15;

    /// <summary>Intervalo de actualización automática de la pantalla de estaciones.</summary>
    public int SegundosActualizacion { get; set; } = 30;

    /// <summary>Tolerancia para relojes adelantados antes de marcar la hora como inconsistente.</summary>
    public int MinutosToleranciaFuturo { get; set; } = 2;
}

/// <summary>Estado derivado de la antigüedad del último heartbeat (no del estado que informa la estación).</summary>
public enum EstadoComunicacion
{
    EnLinea,
    Aviso,
    SinComunicacion,
    NuncaReporto,
    HoraInconsistente,
}

public static class HeartbeatClasificador
{
    public static (int Aviso, int Desconexion) NormalizarUmbrales(int? aviso, int? desconexion, HeartbeatOptions opciones)
    {
        var a = Math.Clamp(aviso ?? opciones.MinutosAviso, 1, 1440);
        var d = Math.Clamp(desconexion ?? opciones.MinutosDesconexion, 2, 10080);
        if (d <= a) d = a + 1;
        return (a, d);
    }

    public static EstadoComunicacion Clasificar(DateTime? ultimoHeartbeat, DateTime ahoraServidor, int minutosAviso, int minutosDesconexion, int toleranciaFuturo = 2)
    {
        if (ultimoHeartbeat is null) return EstadoComunicacion.NuncaReporto;

        var minutos = (ahoraServidor - ultimoHeartbeat.Value).TotalMinutes;
        if (minutos < -toleranciaFuturo) return EstadoComunicacion.HoraInconsistente;
        if (minutos >= minutosDesconexion) return EstadoComunicacion.SinComunicacion;
        if (minutos >= minutosAviso) return EstadoComunicacion.Aviso;
        return EstadoComunicacion.EnLinea;
    }

    public static string Etiqueta(EstadoComunicacion estado) => estado switch
    {
        EstadoComunicacion.EnLinea => "En línea",
        EstadoComunicacion.Aviso => "Aviso: retraso",
        EstadoComunicacion.SinComunicacion => "Sin comunicación",
        EstadoComunicacion.NuncaReporto => "Nunca reportó",
        EstadoComunicacion.HoraInconsistente => "Hora inconsistente",
        _ => estado.ToString(),
    };

    public static string ClaseCss(EstadoComunicacion estado) => estado switch
    {
        EstadoComunicacion.EnLinea => "success",
        EstadoComunicacion.Aviso => "warning",
        EstadoComunicacion.SinComunicacion => "danger",
        EstadoComunicacion.HoraInconsistente => "info",
        _ => "secondary",
    };
}
