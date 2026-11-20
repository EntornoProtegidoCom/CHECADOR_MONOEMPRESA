using System.ComponentModel.DataAnnotations;

namespace ChecadorWeb.Models;

public sealed class EmpleadoFiltro
{
    public string? Texto { get; set; }
    /// <summary>"activos", "inactivos" o vacío para todos.</summary>
    public string? Estado { get; set; } = "activos";
    /// <summary>Solo empleados sin hora de entrada o salida.</summary>
    public bool SinHorario { get; set; }
    public int? Pagina { get; set; }
    public int? Tamano { get; set; }
}

public sealed record EmpleadoFila(
    int Id,
    string Nombre,
    string ApellidoP,
    string ApellidoM,
    string? Correo,
    bool? Activo,
    int VinculosActivos,
    TimeOnly? HoraEntrada,
    TimeOnly? HoraSalidaComida,
    TimeOnly? HoraEntradaComida,
    TimeOnly? HoraSalida,
    DateTime? Actualizado,
    string? ActualizadoPor)
{
    public string NombreCompleto => $"{ApellidoP} {ApellidoM} {Nombre}".Trim();
}

public sealed record EmpleadoOpcion(int Id, string NombreCompleto, bool Activo);

public abstract class EmpleadoDatosBase
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(20, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Nombre(s)")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido paterno es obligatorio.")]
    [StringLength(20, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Apellido paterno")]
    public string ApellidoP { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido materno es obligatorio.")]
    [StringLength(20, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Apellido materno")]
    public string ApellidoM { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Correo no válido.")]
    [StringLength(100, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Correo electrónico")]
    public string? Correo { get; set; }

    [Display(Name = "Empleado Activo")]
    public bool Activo { get; set; } = true;
}

public sealed class EmpleadoAlta : EmpleadoDatosBase
{
    [Required(ErrorMessage = "El ID del trabajador es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El ID del trabajador debe ser un entero mayor que cero.")]
    [Display(Name = "ID de trabajador")]
    public int? IDTrabajador { get; set; }

    // PIN de 4 dígitos: sp_catalogo_trabajadores deriva Contrasena = 'URGL' + CONVERT(varchar(4), PIN).
    [Required(ErrorMessage = "El PIN es obligatorio.")]
    [RegularExpression("^[1-9][0-9]{3}$", ErrorMessage = "El PIN debe tener 4 dígitos y no iniciar con 0.")]
    [DataType(DataType.Password)]
    [Display(Name = "PIN")]
    public string Pin { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme el PIN.")]
    [Compare(nameof(Pin), ErrorMessage = "Los PIN no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar PIN")]
    public string ConfirmarPin { get; set; } = string.Empty;

    public HorarioEdicion Horario { get; set; } = new();
}

public sealed class EmpleadoEdicion : EmpleadoDatosBase
{
    [RegularExpression("^[1-9][0-9]{3}$", ErrorMessage = "El PIN debe tener 4 dígitos y no iniciar con 0.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nuevo PIN (opcional)")]
    public string? NuevoPin { get; set; }

    [Compare(nameof(NuevoPin), ErrorMessage = "Los PIN no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar nuevo PIN")]
    public string? ConfirmarNuevoPin { get; set; }

    /// <summary>Valor de update_time leído (ticks) para detectar ediciones concurrentes.</summary>
    public long? Sello { get; set; }
}

public sealed class HorarioEdicion
{
    [Display(Name = "Entrada")]
    public TimeOnly? Entrada { get; set; }

    [Display(Name = "Salida a comer")]
    public TimeOnly? SalidaComida { get; set; }

    [Display(Name = "Entrada de comer")]
    public TimeOnly? EntradaComida { get; set; }

    [Display(Name = "Salida")]
    public TimeOnly? Salida { get; set; }

    public long? Sello { get; set; }

    public bool EstaVacio => Entrada is null && SalidaComida is null && EntradaComida is null && Salida is null;

    public Services.HorarioCaptura ACaptura() => new(Entrada, SalidaComida, EntradaComida, Salida);
}

public sealed record EmpleadoResumen(int Id, string NombreCompleto, bool? Activo, int VinculosActivos, DateTime? Actualizado, string? ActualizadoPor);
