namespace ChecadorAPI.Models.DTOs;

public class ApiResponseDto<T>
{
    public int Exito { get; set; } // 1 = Exito, 2 = Validacion de negocio / Advertencia, 0 = Error
    public string Mensaje { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ApiResponseDto<T> Ok(T data, string mensaje = "Operacion exitosa.") =>
        new() { Exito = 1, Mensaje = mensaje, Data = data };

    public static ApiResponseDto<T> Warning(string mensaje, T? data = default) =>
        new() { Exito = 2, Mensaje = mensaje, Data = data };

    public static ApiResponseDto<T> Error(string mensaje, T? data = default) =>
        new() { Exito = 0, Mensaje = mensaje, Data = data };
}
