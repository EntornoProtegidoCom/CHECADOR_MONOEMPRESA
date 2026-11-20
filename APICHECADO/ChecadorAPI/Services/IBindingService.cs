using ChecadorAPI.Models.DTOs;

namespace ChecadorAPI.Services;

public interface IBindingService
{
    Task<ApiResponseDto<EquipmentBindingResponseDto>> ObtenerVinculoAsync(string nbEquipo);
    Task<ApiResponseDto<bool>> CrearVinculoAsync(CreateBindingRequestDto req);
    Task<ApiResponseDto<bool>> DesvincularAsync(string nbEquipo, DeleteBindingRequestDto req);
}
