using System.ComponentModel.DataAnnotations;
using ChecadorWeb.Security;

namespace ChecadorWeb.Models;

public sealed class UsuarioFiltro
{
    public string? Texto { get; set; }
    public int? Perfil { get; set; }
    /// <summary>"vigentes", "inactivos" o vacío para todos.</summary>
    public string? Estado { get; set; } = "vigentes";
    public int? Pagina { get; set; }
    public int? Tamano { get; set; }
}

public sealed record UsuarioFila(string Id, int Perfil, string? Nombre, string? Paterno, string? Materno, string? Email, string? Vigente)
{
    public string NombreCompleto => string.Join(' ', new[] { Nombre, Paterno, Materno }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public class UsuarioDatosBase
{
    [Required(ErrorMessage = "Seleccione un perfil.")]
    [Display(Name = "Perfil")]
    public int? IdPerfil { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Nombre(s)")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Apellido paterno")]
    public string? Paterno { get; set; }

    [StringLength(100, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Apellido materno")]
    public string? Materno { get; set; }

    [EmailAddress(ErrorMessage = "Correo no válido.")]
    [StringLength(100, ErrorMessage = "Máximo {1} caracteres.")]
    [Display(Name = "Correo electrónico")]
    public string? Email { get; set; }
}

public sealed class UsuarioAlta : UsuarioDatosBase
{
    [Required(ErrorMessage = "El identificador es obligatorio.")]
    [RegularExpression("^[A-Za-z0-9._-]{3,25}$", ErrorMessage = "Use de 3 a 25 caracteres: letras, números, punto, guion o guion bajo.")]
    [Display(Name = "Usuario (ID_USUARIO)")]
    public string IdUsuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [PasswordSegura]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la contraseña.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmarPassword { get; set; } = string.Empty;
}

public sealed class UsuarioEdicion : UsuarioDatosBase;

public sealed class RestablecerPassword
{
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [PasswordSegura]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string Nueva { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la contraseña.")]
    [Compare(nameof(Nueva), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar contraseña")]
    public string Confirmar { get; set; } = string.Empty;
}

public sealed class CambiarPasswordPropia
{
    [Required(ErrorMessage = "Capture su contraseña actual.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña actual")]
    public string Actual { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [PasswordSegura]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string Nueva { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la contraseña.")]
    [Compare(nameof(Nueva), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar contraseña")]
    public string Confirmar { get; set; } = string.Empty;
}

/// <summary>
/// Política del sitio para contraseñas nuevas. No cambia el formato de almacenamiento;
/// el máximo de 50 caracteres proviene de sp_seguridad_usuarios (nvarchar(50)).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PasswordSeguraAttribute : ValidationAttribute
{
    public const int Minimo = 8;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string s || s.Length == 0) return ValidationResult.Success;
        if (s.Length < Minimo || s.Length > LegacyPasswordHasher.LongitudMaxima)
            return new ValidationResult($"La contraseña debe tener entre {Minimo} y {LegacyPasswordHasher.LongitudMaxima} caracteres.");
        if (!s.Any(char.IsLetter) || !s.Any(char.IsDigit))
            return new ValidationResult("La contraseña debe incluir letras y números.");
        if (s.Trim() != s)
            return new ValidationResult("La contraseña no debe iniciar ni terminar con espacios.");
        return ValidationResult.Success;
    }
}
