using System.ComponentModel.DataAnnotations;

namespace ChecadorWeb.Models;

// ─── Fila del listado ───────────────────────────────────────────────────────

public sealed record DiaFestivoFila(
    int Id,
    DateOnly Fecha,
    string Descripcion,
    bool EsOficial,
    bool Estatus,
    DateTime? UpdateTime,
    string? UpdateUsuario);

// ─── Formulario de captura / edición ────────────────────────────────────────

public sealed class DiaFestivoCaptura
{
    public int Id { get; set; }                           // 0 = Alta, > 0 = Edición

    [Required(ErrorMessage = "La fecha es obligatoria.")]
    public DateOnly? Fecha { get; set; }

    [Required(ErrorMessage = "El concepto es obligatorio."), MaxLength(150)]
    public string Descripcion { get; set; } = string.Empty;

    public bool EsOficial { get; set; } = true;
}
