namespace ChecadorAPI.Models.DTOs;

public class WorkerLoginRequestDto
{
    public int IdTrabajador { get; set; }
    public int Pin { get; set; }
}

public class WorkerLoginResponseDto
{
    public int IdTrabajador { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string? HoraEntrada { get; set; }
    public string? HoraSalidaComida { get; set; }
    public string? HoraEntradaComida { get; set; }
    public string? HoraSalida { get; set; }
    public bool Status { get; set; }
}

public class AdminLoginRequestDto
{
    public string IdUsuario { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AdminLoginResponseDto
{
    public string IdUsuario { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int IdPerfil { get; set; }
    public string StVigente { get; set; } = string.Empty;
}
