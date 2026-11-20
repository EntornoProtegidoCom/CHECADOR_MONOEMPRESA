namespace ChecadorDesktop.Models;

// --- MODELOS LOCALES ---
public class EstacionVinculoLocal
{
    public string NbEquipo { get; set; } = Environment.MachineName;
    public int IdTrabajador { get; set; }
    public string NombreTrabajador { get; set; } = string.Empty;
    public DateTime FechaVinculacion { get; set; } = DateTime.Now;
    public DateTime VigenteHasta { get; set; } = DateTime.MaxValue;
    public bool Activo { get; set; } = true;

    public bool EstaVigente => Activo;
}

public class RegistroAsistenciaLocal
{
    public long IdLocal { get; set; }
    public string GuidRegistro { get; set; } = Guid.NewGuid().ToString();
    public int IDTrabajador { get; set; }
    public int PIN { get; set; }
    public DateTime FechaHoraCaptura { get; set; }
    public int TipoRegistro { get; set; } // 1: Entrada, 2: SalidaComer, 3: EntradaComida, 4: Salida
    public string IPRegistro { get; set; } = "127.0.0.1";
    public string NBEquipo { get; set; } = Environment.MachineName;
    public byte[] Foto { get; set; } = Array.Empty<byte>();
    public bool EsFotoReal { get; set; }
    public string? MotivoFallback { get; set; }
    public string? CameraStatus { get; set; }
    public string? CameraDeviceId { get; set; }
    public string HashLocal { get; set; } = string.Empty;
    public int EstadoSync { get; set; } // 0: Pendiente, 1: Enviando, 2: Sincronizado, 3: Error
    public int IntentosSync { get; set; }
    public string? UltimoError { get; set; }
    public DateTime? FechaHoraSincronizacion { get; set; }
}

public class WorkerCacheItem
{
    public int IdTrabajador { get; set; }
    public int Pin { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string? HoraEntrada { get; set; }
    public string? HoraSalidaComida { get; set; }
    public string? HoraEntradaComida { get; set; }
    public string? HoraSalida { get; set; }
}

public class WorkerSessionLocal
{
    public string NbEquipo { get; set; } = Environment.MachineName;
    public int IdTrabajador { get; set; }
    public int Pin { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string? HoraEntrada { get; set; }
    public string? HoraSalidaComida { get; set; }
    public string? HoraEntradaComida { get; set; }
    public string? HoraSalida { get; set; }
    public DateTime FechaInicio { get; set; } = DateTime.Now;
    public DateTime FechaUltimaActividad { get; set; } = DateTime.Now;
    public int TipoRegistroUltimo { get; set; }
    public bool Activa { get; set; } = true;
}

// --- DTOs API ---
public class ApiResponseDto<T>
{
    public int Exito { get; set; } // 1: Éxito, 2: Regla de negocio, 0: Error
    public string Mensaje { get; set; } = string.Empty;
    public T? Data { get; set; }
}

public record WorkerLoginRequestDto(int IdTrabajador, int Pin);

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

/// <summary>
/// DTO de respuesta del endpoint GET /api/workers/{id}/schedule.
/// Contiene la identidad y los horarios asignados al trabajador en el servidor.
/// </summary>
public class WorkerScheduleResponseDto
{
    public int IdTrabajador { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string? HoraEntrada { get; set; }
    public string? HoraSalidaComida { get; set; }
    public string? HoraEntradaComida { get; set; }
    public string? HoraSalida { get; set; }
}

public record AdminLoginRequestDto(string IdUsuario, string Password);
public class AdminLoginResponseDto
{
    public string IdUsuario { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int IdPerfil { get; set; }
}

public class EquipmentBindingResponseDto
{
    public int Id { get; set; }
    public string NbEquipo { get; set; } = string.Empty;
    public int IdTrabajador { get; set; }
    public bool Activo { get; set; }
}

public record CreateBindingRequestDto(string NbEquipo, int IdTrabajador, string UpdateUsuario);
public record DeleteBindingRequestDto(string UpdateUsuario);
public record HeartbeatRequestDto(string NbEquipo, int IdTrabajador, string IpRegistro, string VersionCliente);

public class MarcajeRequestDto
{
    public string GuidRegistro { get; set; } = string.Empty;
    public int IdTrabajador { get; set; }
    public int Pin { get; set; }
    public DateTime FechaHoraCaptura { get; set; }
    public int TipoRegistro { get; set; }
    public string IpRegistro { get; set; } = "127.0.0.1";
    public string NbEquipo { get; set; } = string.Empty;
    public string? FotoBase64 { get; set; }
}

public class MarcajeResponseDto
{
    public int IdTrabajador { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Horaregistro { get; set; } = string.Empty;
    public string TipoRegistro { get; set; } = string.Empty;
    public string HashRegistroHex { get; set; } = string.Empty;
}

public class MarcajeBatchRequestDto
{
    public List<MarcajeRequestDto> Registros { get; set; } = new();
}

public class MarcajeBatchResultDto
{
    public int TotalProcesados { get; set; }
    public int Exitosos { get; set; }
    public int Fallidos { get; set; }
    public List<string> Mensajes { get; set; } = new();
}
