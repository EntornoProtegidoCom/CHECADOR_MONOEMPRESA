using System.Data;
using Microsoft.Data.SqlClient;
using ChecadorAPI.Data;
using ChecadorAPI.Models.DTOs;

namespace ChecadorAPI.Services;

public class BindingService : IBindingService
{
    private readonly ISqlConnectionFactory _dbFactory;

    public BindingService(ISqlConnectionFactory dbFactory) => _dbFactory = dbFactory;

    public async Task<ApiResponseDto<EquipmentBindingResponseDto>> ObtenerVinculoAsync(string nbEquipo)
    {
        var xml = XmlPayloadBuilder.BuildXml("C", new() { ["NBEquipo"] = nbEquipo });

        using var conn = (SqlConnection)_dbFactory.CreateConnection();
        using var cmd = new SqlCommand("sp_desktop_vinculo_equipo", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add(new SqlParameter("@param", SqlDbType.NText) { Value = xml });

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return ApiResponseDto<EquipmentBindingResponseDto>.Warning("Equipo no vinculado.");

        int exito = Convert.ToInt32(reader["EXITO"]);
        if (exito != 1)
            return ApiResponseDto<EquipmentBindingResponseDto>.Warning(reader["MENSAJE"]?.ToString() ?? "Sin vinculo activo.");

        var data = new EquipmentBindingResponseDto
        {
            Id = Convert.ToInt32(reader["ID"]),
            NbEquipo = reader["NBEquipo"].ToString() ?? nbEquipo,
            IdTrabajador = Convert.ToInt32(reader["IDTrabajador"]),
            FechaRegistro = Convert.ToDateTime(reader["FechaRegistro"]),
            Activo = Convert.ToBoolean(reader["Activo"]),
            UpdateUsuario = reader["Update_ID_USUARIO"]?.ToString() ?? string.Empty,
            UpdateFecha = Convert.ToDateTime(reader["Update_Fecha"])
        };

        return ApiResponseDto<EquipmentBindingResponseDto>.Ok(data, "Equipo vinculado.");
    }

    public async Task<ApiResponseDto<bool>> CrearVinculoAsync(CreateBindingRequestDto req)
    {
        var xml = XmlPayloadBuilder.BuildXml("I", new()
        {
            ["NBEquipo"] = req.NbEquipo,
            ["IDTrabajador"] = req.IdTrabajador,
            ["Update_ID_USUARIO"] = req.UpdateUsuario
        });

        using var conn = (SqlConnection)_dbFactory.CreateConnection();
        using var cmd = new SqlCommand("sp_desktop_vinculo_equipo", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add(new SqlParameter("@param", SqlDbType.NText) { Value = xml });

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int exito = Convert.ToInt32(reader["EXITO"]);
            string mensaje = reader["MENSAJE"]?.ToString() ?? string.Empty;
            return exito == 1 ? ApiResponseDto<bool>.Ok(true, mensaje) : ApiResponseDto<bool>.Warning(mensaje, false);
        }

        return ApiResponseDto<bool>.Error("No se recibio respuesta de la BD.");
    }

    public async Task<ApiResponseDto<bool>> DesvincularAsync(string nbEquipo, DeleteBindingRequestDto req)
    {
        var xml = XmlPayloadBuilder.BuildXml("D", new()
        {
            ["NBEquipo"] = nbEquipo,
            ["Update_ID_USUARIO"] = req.UpdateUsuario
        });

        using var conn = (SqlConnection)_dbFactory.CreateConnection();
        using var cmd = new SqlCommand("sp_desktop_vinculo_equipo", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add(new SqlParameter("@param", SqlDbType.NText) { Value = xml });

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            int exito = Convert.ToInt32(reader["EXITO"]);
            string mensaje = reader["MENSAJE"]?.ToString() ?? string.Empty;
            return exito == 1 ? ApiResponseDto<bool>.Ok(true, mensaje) : ApiResponseDto<bool>.Warning(mensaje, false);
        }

        return ApiResponseDto<bool>.Error("Error al desvincular equipo.");
    }
}
