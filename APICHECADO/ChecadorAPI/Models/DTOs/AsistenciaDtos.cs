namespace ChecadorAPI.Models.DTOs;

public class MarcajeRequestDto
{
    public string GuidRegistro { get; set; } = string.Empty;
    public int IdTrabajador { get; set; }
    public int? Pin { get; set; }
    public DateTime FechaHoraCaptura { get; set; }
    public int TipoRegistro { get; set; } // 1: Entrada, 2: SalidaComer, 3: EntradaComida, 4: Salida
    public string IpRegistro { get; set; } = "127.0.0.1";
    public string NbEquipo { get; set; } = string.Empty;
    public string? FotoBase64 { get; set; }
}

public class MarcajeResponseDto
{
    public int IdTrabajador { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
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
