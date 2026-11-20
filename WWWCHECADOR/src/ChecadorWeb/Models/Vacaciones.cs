using System.ComponentModel.DataAnnotations;

namespace ChecadorWeb.Models;

public sealed record VacacionFila(
    int Id,
    int IdTrabajador,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    int DiasTomados,
    int? PeriodoAnio,
    string? Observaciones,
    bool Estatus,
    DateTime CreateTime,
    string? CreateUsuario,
    DateTime UpdateTime,
    string? UpdateUsuario);

public sealed class VacacionAlta
{
    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    [Display(Name = "Fecha de inicio")]
    public DateOnly? FechaInicio { get; set; }

    [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
    [Display(Name = "Fecha de fin")]
    public DateOnly? FechaFin { get; set; }

    [Required(ErrorMessage = "Los días tomados son obligatorios.")]
    [Range(1, 365, ErrorMessage = "Los días tomados deben ser al menos 1.")]
    [Display(Name = "Días tomados")]
    public int? DiasTomados { get; set; }

    [Range(2000, 2100, ErrorMessage = "Año fuera de rango.")]
    [Display(Name = "Periodo / Año")]
    public int? PeriodoAnio { get; set; }

    [StringLength(250, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }
}
