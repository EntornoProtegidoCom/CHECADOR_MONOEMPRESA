using ChecadorAPI.Models.DTOs;

namespace ChecadorAPI.Services;

public interface IWorkerScheduleService
{
    Task<ApiResponseDto<WorkerScheduleResponseDto>> GetScheduleAsync(int idTrabajador, CancellationToken cancellationToken);
}