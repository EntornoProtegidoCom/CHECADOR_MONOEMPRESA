namespace ChecadorAPI.Models.DTOs;

public record CreateBindingRequestDto(string NbEquipo, int IdTrabajador, string UpdateUsuario);
public record DeleteBindingRequestDto(string UpdateUsuario);

public class EquipmentBindingResponseDto
{
    public int Id { get; set; }
    public string NbEquipo { get; set; } = string.Empty;
    public int IdTrabajador { get; set; }
    public DateTime FechaRegistro { get; set; }
    public bool Activo { get; set; }
    public string UpdateUsuario { get; set; } = string.Empty;
    public DateTime UpdateFecha { get; set; }
}
