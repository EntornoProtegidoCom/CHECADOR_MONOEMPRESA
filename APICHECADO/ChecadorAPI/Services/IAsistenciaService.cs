using ChecadorAPI.Models.DTOs;

namespace ChecadorAPI.Services;

public interface IAsistenciaService
{
    Task<ApiResponseDto<MarcajeResponseDto>> RegistrarMarcajeAsync(MarcajeRequestDto req);
    Task<ApiResponseDto<MarcajeBatchResultDto>> ProcesarLoteOfflineAsync(MarcajeBatchRequestDto batch);
}
