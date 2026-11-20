using System.ComponentModel.DataAnnotations;

namespace ChecadorWeb.Models;

public sealed class VinculoFiltro
{
    public string? Texto { get; set; }
    /// <summary>"activos", "inactivos" o vacío para todos.</summary>
    public string? Estado { get; set; } = "activos";
    public int? Pagina { get; set; }
    public int? Tamano { get; set; }
}

public sealed record VinculoFila(
    int Id,
    string NbEquipo,
    int IdTrabajador,
    string NombreTrabajador,
    bool? TrabajadorActivo,
    bool Activo,
    DateTime Creado,
    string? CreadoPor,
    DateTime Actualizado,
    string? ActualizadoPor,
    DateTime? UltimoHeartbeat);

public sealed class VinculoAlta
{
    // DD_EQUIPO_VINCULO admite 100, pero MM_HORARIO.NBEquipo y sp_registro_checador usan 50.
    [Required(ErrorMessage = "El nombre del equipo es obligatorio.")]
    [StringLength(50, ErrorMessage = "Máximo {1} caracteres (límite de MM_HORARIO.NBEquipo).")]
    [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9._-]*$", ErrorMessage = "Use el nombre de equipo de Windows: letras, números, punto o guiones.")]
    [Display(Name = "Nombre del equipo (NBEquipo)")]
    public string NbEquipo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleccione un empleado.")]
    [Display(Name = "Empleado")]
    public int? IdTrabajador { get; set; }

    [Display(Name = "Confirmo que el empleado tendrá más de un equipo vinculado")]
    public bool ConfirmarMultiple { get; set; }
}

public enum TipoConflicto
{
    EquipoConVariosVinculosActivos,
    TrabajadorConVariosEquipos,
    VinculoConTrabajadorInactivo,
    HeartbeatSinVinculo,
    HeartbeatTrabajadorDistinto,
}

public sealed record ConflictoVinculo(TipoConflicto Tipo, string NbEquipo, string Descripcion, bool Critico);
