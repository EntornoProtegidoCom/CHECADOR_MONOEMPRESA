using System.ComponentModel.DataAnnotations;

namespace ChecadorWeb.Models;

public sealed class ResumenHorasFiltro : IValidatableObject
{
    public const int DiasMaximos = 366;

    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }

    [StringLength(100, ErrorMessage = "El empleado debe tener como máximo 100 caracteres.")]
    public string? Trabajador { get; set; }

    public void AplicarPredeterminados()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var lunes = hoy.AddDays(-((int)hoy.DayOfWeek + 6) % 7);
        Desde ??= lunes;
        Hasta ??= lunes.AddDays(4);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Desde < new DateOnly(1753, 1, 1) || Hasta < new DateOnly(1753, 1, 1))
            yield return new ValidationResult("Las fechas deben ser iguales o posteriores al 01/01/1753.",
                [nameof(Desde), nameof(Hasta)]);
        if (Desde is { } desde && Hasta is { } hasta)
        {
            if (desde > hasta)
                yield return new ValidationResult("Desde no puede ser posterior a Hasta.", [nameof(Desde), nameof(Hasta)]);
            else if (hasta.DayNumber - desde.DayNumber + 1 > DiasMaximos)
                yield return new ValidationResult($"El rango máximo es de {DiasMaximos} días, incluyendo ambas fechas.",
                    [nameof(Desde), nameof(Hasta)]);
            else if (hasta.DayNumber - desde.DayNumber <= 1
                && !ReporteHorasResultado.EsDiaEntreSemana(desde.ToDateTime(TimeOnly.MinValue))
                && !ReporteHorasResultado.EsDiaEntreSemana(hasta.ToDateTime(TimeOnly.MinValue)))
                yield return new ValidationResult("Seleccione un rango que incluya al menos un día de lunes a viernes.",
                    [nameof(Desde), nameof(Hasta)]);
        }
    }
}

public sealed class ResumenHorasEmpleadoDto
{
    public int IdTrabajador { get; set; }
    public string NombreTrabajador { get; set; } = string.Empty;
    public Dictionary<DateTime, DetalleJornadaDiaDto> Dias { get; set; } = new();

    private IEnumerable<KeyValuePair<DateTime, DetalleJornadaDiaDto>> DiasEntreSemana =>
        Dias.Where(d => ReporteHorasResultado.EsDiaEntreSemana(d.Key));

    public double TotalHorasSemanales => DiasEntreSemana.Where(d => !d.Value.EsIncompleto).Sum(d => d.Value.HorasEfectivas);
    public bool TieneOmisiones => DiasEntreSemana.Any(d => d.Value.EsIncompleto);
    public Dictionary<DateTime, double> TotalesPorSemana => DiasEntreSemana
        .GroupBy(d => ReporteHorasResultado.InicioSemana(d.Key))
        .ToDictionary(g => g.Key, g => g.Where(d => !d.Value.EsIncompleto).Sum(d => d.Value.HorasEfectivas));
}

public sealed class DetalleJornadaDiaDto
{
    public double HorasEfectivas { get; set; }
    public TimeSpan? TiempoComida { get; set; }
    public bool EsIncompleto { get; set; }
    public string Estatus { get; set; } = "OK";
}

public sealed record ReporteHorasResultado(
    DateOnly Desde, DateOnly Hasta, IReadOnlyList<ResumenHorasEmpleadoDto> Empleados)
{
    public IReadOnlyList<DateTime> Fechas => Enumerable.Range(0, Hasta.DayNumber - Desde.DayNumber + 1)
        .Select(i => Desde.AddDays(i).ToDateTime(TimeOnly.MinValue)).Where(EsDiaEntreSemana).ToArray();

    public IReadOnlyList<DateTime> Semanas => Fechas.Select(InicioSemana).Distinct().ToArray();

    public static DateTime InicioSemana(DateTime fecha) =>
        fecha.Date.AddDays(-((int)fecha.DayOfWeek + 6) % 7);

    public static bool EsDiaEntreSemana(DateTime fecha) =>
        fecha.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
}
