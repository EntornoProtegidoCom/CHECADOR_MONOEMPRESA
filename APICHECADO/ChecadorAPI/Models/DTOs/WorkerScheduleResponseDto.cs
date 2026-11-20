namespace ChecadorAPI.Models.DTOs;

/// <summary>Horarios oficiales del trabajador, sin credenciales ni datos sensibles.</summary>
public sealed record WorkerScheduleResponseDto(
    int IdTrabajador,
    string NombreCompleto,
    string HoraEntrada,
    string HoraSalidaComida,
    string HoraEntradaComida,
    string HoraSalida);