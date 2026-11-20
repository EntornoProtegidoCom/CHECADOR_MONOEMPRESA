using System.Globalization;
using ChecadorAPI.Models.DTOs;
using ChecadorAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChecadorAPI.Controllers;

[ApiController]
[Route("api/workers")]
public sealed class WorkersController(IWorkerScheduleService service, ILogger<WorkersController> logger) : ControllerBase
{
    [HttpGet("{idTrabajador}/schedule")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponseDto<WorkerScheduleResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<WorkerScheduleResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<WorkerScheduleResponseDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<WorkerScheduleResponseDto>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponseDto<WorkerScheduleResponseDto>>> GetSchedule(
        [FromRoute] string idTrabajador, CancellationToken cancellationToken)
    {
        if (!int.TryParse(idTrabajador, NumberStyles.Integer, CultureInfo.InvariantCulture, out var workerId) || workerId <= 0)
        {
            return BadRequest(ApiResponseDto<WorkerScheduleResponseDto>.Error("idTrabajador debe ser un entero mayor a 0."));
        }

        try
        {
            var result = await service.GetScheduleAsync(workerId, cancellationToken);
            return result.Exito switch
            {
                1 => Ok(result),
                2 => BadRequest(result),
                _ => StatusCode(StatusCodes.Status500InternalServerError, result)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error al consultar horarios del trabajador {IdTrabajador}", workerId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponseDto<WorkerScheduleResponseDto>.Error("No fue posible obtener los horarios del trabajador."));
        }
    }
}