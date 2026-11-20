using ChecadorAPI.Models.DTOs;

namespace ChecadorAPI.Services;

public interface IAuthService
{
    Task<ApiResponseDto<WorkerLoginResponseDto>> LoginTrabajadorAsync(WorkerLoginRequestDto req);
    Task<ApiResponseDto<AdminLoginResponseDto>> LoginAdminAsync(AdminLoginRequestDto req);
}
