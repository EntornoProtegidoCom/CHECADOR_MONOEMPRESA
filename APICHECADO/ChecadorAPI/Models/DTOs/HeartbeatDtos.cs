namespace ChecadorAPI.Models.DTOs;

public record HeartbeatRequestDto(
    string NbEquipo,
    int IdTrabajador,
    string? IpRegistro,
    string? VersionApp
);
