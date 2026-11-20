using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ChecadorDesktop.Models;

namespace ChecadorDesktop.Services;

public class ApiClientService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _apiKey;

    public ApiClientService()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        _baseUrl = config["ApiSettings:BaseUrl"] ?? "http://localhost:8080/";
        _apiKey = config["ApiSettings:ApiKey"] ?? string.Empty;
        int timeoutSec = int.TryParse(config["ApiSettings:TimeoutSeconds"], out int t) ? t : 15;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(timeoutSec)
        };
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // --- HEALTH CHECK ---
    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var res = await _httpClient.GetAsync("api/health");
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // --- AUTENTICACIÓN ---
    public async Task<ApiResponseDto<WorkerLoginResponseDto>?> WorkerLoginAsync(int idTrabajador, int pin)
    {
        var payload = new WorkerLoginRequestDto(idTrabajador, pin);
        return await PostJsonAsync<WorkerLoginRequestDto, WorkerLoginResponseDto>("api/auth/worker-login", payload);
    }

    public async Task<ApiResponseDto<AdminLoginResponseDto>?> AdminLoginAsync(string idUsuario, string password)
    {
        var payload = new AdminLoginRequestDto(idUsuario, password);
        return await PostJsonAsync<AdminLoginRequestDto, AdminLoginResponseDto>("api/auth/admin-login", payload);
    }

    // --- HORARIOS DEL TRABAJADOR ---
    /// <summary>
    /// Obtiene los horarios asignados a un trabajador desde el servidor sin requerir PIN.
    /// Autenticado únicamente con el X-Api-Key de la estación.
    /// Endpoint: GET /api/workers/{idTrabajador}/schedule
    /// </summary>
    public async Task<ApiResponseDto<WorkerScheduleResponseDto>?> GetWorkerScheduleAsync(int idTrabajador)
    {
        try
        {
            var res = await _httpClient.GetAsync($"api/workers/{idTrabajador}/schedule");
            string resJson = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                return new ApiResponseDto<WorkerScheduleResponseDto>
                {
                    Exito = 0,
                    Mensaje = $"Error HTTP {(int)res.StatusCode}: {ExtraerMensajeSeguro(resJson)}"
                };
            }

            var parsed = TryDeserializeApiResponse<WorkerScheduleResponseDto>(resJson);
            if (parsed != null)
            {
                return parsed;
            }

            return new ApiResponseDto<WorkerScheduleResponseDto>
            {
                Exito = 0,
                Mensaje = $"La API no devolvio JSON valido. Respuesta: {ExtraerMensajeSeguro(resJson)}"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponseDto<WorkerScheduleResponseDto> { Exito = 0, Mensaje = ex.Message };
        }
    }

    // --- VÍNCULOS DE ESTACIÓN ---
    public async Task<ApiResponseDto<EquipmentBindingResponseDto>?> GetBindingAsync(string nbEquipo)
    {
        try
        {
            var res = await _httpClient.GetAsync($"api/bindings/{Uri.EscapeDataString(nbEquipo)}");
            string resJson = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                return new ApiResponseDto<EquipmentBindingResponseDto>
                {
                    Exito = 0,
                    Mensaje = $"Error HTTP {(int)res.StatusCode}: {ExtraerMensajeSeguro(resJson)}"
                };
            }

            var parsed = TryDeserializeApiResponse<EquipmentBindingResponseDto>(resJson);
            if (parsed != null)
            {
                return parsed;
            }

            return new ApiResponseDto<EquipmentBindingResponseDto>
            {
                Exito = 0,
                Mensaje = $"La API no devolvio JSON valido. Respuesta: {ExtraerMensajeSeguro(resJson)}"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponseDto<EquipmentBindingResponseDto> { Exito = 0, Mensaje = ex.Message };
        }
    }

    public async Task<ApiResponseDto<bool>?> CreateBindingAsync(string nbEquipo, int idTrabajador, string updateUsuario)
    {
        var payload = new CreateBindingRequestDto(nbEquipo, idTrabajador, updateUsuario);
        return await PostJsonAsync<CreateBindingRequestDto, bool>("api/bindings", payload);
    }

    public async Task<ApiResponseDto<bool>?> DeleteBindingAsync(string nbEquipo, string updateUsuario)
    {
        try
        {
            var payload = new DeleteBindingRequestDto(updateUsuario);
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var req = new HttpRequestMessage(HttpMethod.Delete, $"api/bindings/{Uri.EscapeDataString(nbEquipo)}")
            {
                Content = content
            };
            var res = await _httpClient.SendAsync(req);
            string resJson = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                return new ApiResponseDto<bool>
                {
                    Exito = 0,
                    Mensaje = $"Error HTTP {(int)res.StatusCode}: {ExtraerMensajeSeguro(resJson)}",
                    Data = false
                };
            }

            var parsed = TryDeserializeApiResponse<bool>(resJson);
            if (parsed != null)
            {
                return parsed;
            }

            return new ApiResponseDto<bool>
            {
                Exito = 0,
                Mensaje = $"La API no devolvio JSON valido. Respuesta: {ExtraerMensajeSeguro(resJson)}",
                Data = false
            };
        }
        catch (Exception ex)
        {
            return new ApiResponseDto<bool> { Exito = 0, Mensaje = ex.Message, Data = false };
        }
    }

    // --- ASISTENCIA Y MARCAJES ---
    public async Task<ApiResponseDto<MarcajeResponseDto>?> RegistrarMarcajeOnlineAsync(MarcajeRequestDto req)
    {
        return await PostJsonAsync<MarcajeRequestDto, MarcajeResponseDto>("api/asistencia/marcaje/online", req);
    }

    public async Task<ApiResponseDto<MarcajeBatchResultDto>?> RegistrarMarcajeOfflineBatchAsync(List<MarcajeRequestDto> registros)
    {
        var payload = new MarcajeBatchRequestDto { Registros = registros };
        return await PostJsonAsync<MarcajeBatchRequestDto, MarcajeBatchResultDto>("api/asistencia/marcaje/offline", payload);
    }

    public async Task<ApiResponseDto<bool>?> SendHeartbeatAsync(string nbEquipo, int idTrabajador)
    {
        var payload = new HeartbeatRequestDto(nbEquipo, idTrabajador, "127.0.0.1", "1.0.0");
        return await PostJsonAsync<HeartbeatRequestDto, bool>("api/asistencia/heartbeat", payload);
    }

    // Helper genérico para peticiones POST
    private async Task<ApiResponseDto<TOut>?> PostJsonAsync<TIn, TOut>(string endpoint, TIn data)
    {
        try
        {
            string jsonStr = JsonConvert.SerializeObject(data);
            var content = new StringContent(jsonStr, Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync(endpoint, content);
            string resJson = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                var apiResponse = TryDeserializeApiResponse<TOut>(resJson);
                if (apiResponse != null)
                {
                    return apiResponse;
                }

                return new ApiResponseDto<TOut>
                {
                    Exito = 0,
                    Mensaje = $"Error HTTP {(int)res.StatusCode}: {ExtraerMensajeSeguro(resJson)}"
                };
            }

            var parsed = TryDeserializeApiResponse<TOut>(resJson);
            if (parsed != null)
            {
                return parsed;
            }

            return new ApiResponseDto<TOut>
            {
                Exito = 0,
                Mensaje = $"La API no devolvio JSON valido. Respuesta: {ExtraerMensajeSeguro(resJson)}"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponseDto<TOut> { Exito = 0, Mensaje = ex.Message };
        }
    }

    private static ApiResponseDto<T>? TryDeserializeApiResponse<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var token = JToken.Parse(json);
            if (token.Type != JTokenType.Object)
            {
                return null;
            }

            return token.ToObject<ApiResponseDto<T>>();
        }
        catch
        {
            return null;
        }
    }

    private static string ExtraerMensajeSeguro(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Sin contenido en la respuesta";
        }

        string limpio = content.Replace("\r", " ").Replace("\n", " ").Trim();
        return limpio.Length <= 180 ? limpio : limpio[..180] + "...";
    }
}
