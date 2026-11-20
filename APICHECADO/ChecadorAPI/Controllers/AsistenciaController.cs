using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using ChecadorAPI.Data;
using ChecadorAPI.Models.DTOs;
using ChecadorAPI.Services;

namespace ChecadorAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AsistenciaController : ControllerBase
{
    private readonly IAsistenciaService _asistenciaService;
    private readonly ISqlConnectionFactory _dbFactory;

    public AsistenciaController(IAsistenciaService asistenciaService, ISqlConnectionFactory dbFactory)
    {
        _asistenciaService = asistenciaService;
        _dbFactory = dbFactory;
    }

    [HttpPost("marcaje/online")]
    public async Task<ActionResult<ApiResponseDto<MarcajeResponseDto>>> MarcajeOnline([FromBody] MarcajeRequestDto req)
    {
        var result = await _asistenciaService.RegistrarMarcajeAsync(req);
        return result.Exito switch
        {
            1 => Ok(result),
            2 => BadRequest(result),
            _ => StatusCode(StatusCodes.Status500InternalServerError, result)
        };
    }

    [HttpPost("marcaje/offline")]
    public async Task<ActionResult<ApiResponseDto<MarcajeBatchResultDto>>> MarcajeOffline([FromBody] MarcajeBatchRequestDto batch)
    {
        var result = await _asistenciaService.ProcesarLoteOfflineAsync(batch);
        return Ok(result);
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> RegistrarHeartbeat([FromBody] HeartbeatRequestDto req)
    {
        if (string.IsNullOrWhiteSpace(req.NbEquipo) || req.IdTrabajador <= 0)
        {
            return BadRequest(ApiResponseDto<bool>.Warning("Datos de heartbeat invalidos."));
        }

        try
        {
            using var conn = (SqlConnection)_dbFactory.CreateConnection();
            using var cmd = new SqlCommand("sp_desktop_registrar_heartbeat", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.Add(new SqlParameter("@NBEquipo", SqlDbType.VarChar, 100) { Value = req.NbEquipo });
            cmd.Parameters.Add(new SqlParameter("@IDTrabajador", SqlDbType.Int) { Value = req.IdTrabajador });
            cmd.Parameters.Add(new SqlParameter("@IPRegistro", SqlDbType.VarChar, 50) { Value = (object?)req.IpRegistro ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@VersionApp", SqlDbType.VarChar, 20) { Value = (object?)req.VersionApp ?? DBNull.Value });

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            return Ok(ApiResponseDto<bool>.Ok(true, "Heartbeat recibido."));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponseDto<bool>.Error($"Error procesando heartbeat: {ex.Message}"));
        }
    }
}
