using System.Data;
using Microsoft.Data.SqlClient;
using ChecadorAPI.Data;
using ChecadorAPI.Models.DTOs;

namespace ChecadorAPI.Services;

public class AuthService : IAuthService
{
    private readonly ISqlConnectionFactory _dbFactory;

    public AuthService(ISqlConnectionFactory dbFactory) => _dbFactory = dbFactory;

    public async Task<ApiResponseDto<WorkerLoginResponseDto>> LoginTrabajadorAsync(WorkerLoginRequestDto req)
    {
        var xml = XmlPayloadBuilder.BuildXml("C", new()
        {
            ["IDTrabajador"] = req.IdTrabajador,
            ["PIN"] = req.Pin
        });

        using var conn = (SqlConnection)_dbFactory.CreateConnection();
        using var cmd = new SqlCommand("sp_desktop_login_trabajador", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add(new SqlParameter("@param", SqlDbType.NText) { Value = xml });

        await conn.OpenAsync();
        using var adapter = new SqlDataAdapter(cmd);
        var ds = new DataSet();
        adapter.Fill(ds);

        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
        {
            return ApiResponseDto<WorkerLoginResponseDto>.Warning("Credenciales incorrectas o usuario inactivo.");
        }

        var statusRow = ds.Tables[0].Rows[0];
        var exito = Convert.ToInt32(statusRow["EXITO"]);
        if (exito != 1)
        {
            return ApiResponseDto<WorkerLoginResponseDto>.Warning(statusRow["MENSAJE"]?.ToString() ?? "Acceso denegado.");
        }

        var dataRow = ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0 ? ds.Tables[1].Rows[0] : statusRow;
        var data = new WorkerLoginResponseDto
        {
            IdTrabajador = Convert.ToInt32(dataRow["IDTrabajador"]),
            NombreCompleto = $"{dataRow["Nombre"]} {dataRow["ApellidoP"]} {dataRow["ApellidoM"]}".Trim(),
            HoraEntrada = dataRow["HoraEntrada"]?.ToString(),
            HoraSalidaComida = dataRow["HoraSalidaComida"]?.ToString(),
            HoraEntradaComida = dataRow["HoraEntradaComida"]?.ToString(),
            HoraSalida = dataRow["HoraSalida"]?.ToString(),
            Status = Convert.ToBoolean(dataRow["status"])
        };

        return ApiResponseDto<WorkerLoginResponseDto>.Ok(data, "Login correcto.");
    }

    public async Task<ApiResponseDto<AdminLoginResponseDto>> LoginAdminAsync(AdminLoginRequestDto req)
    {
        var xml = XmlPayloadBuilder.BuildXml("C", new()
        {
            ["ID_USUARIO"] = req.IdUsuario,
            ["Password"] = req.Password
        });

        using var conn = (SqlConnection)_dbFactory.CreateConnection();
        using var cmd = new SqlCommand("sp_desktop_login_admin", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add(new SqlParameter("@param", SqlDbType.NText) { Value = xml });

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync();
        if (!reader.HasRows)
        {
            return ApiResponseDto<AdminLoginResponseDto>.Warning("Usuario no encontrado.");
        }

        await reader.ReadAsync();
        var exito = Convert.ToInt32(reader["EXITO"]);
        if (exito != 1)
        {
            return ApiResponseDto<AdminLoginResponseDto>.Warning(reader["MENSAJE"]?.ToString() ?? "Contrasena incorrecta o permisos insuficientes.");
        }

        var data = new AdminLoginResponseDto
        {
            IdUsuario = reader["ID_USUARIO"].ToString() ?? req.IdUsuario,
            Nombre = reader["NOMBRE"].ToString() ?? string.Empty,
            IdPerfil = Convert.ToInt32(reader["ID_PERFIL"]),
            StVigente = reader["ST_VIGENTE"].ToString() ?? "A"
        };

        return ApiResponseDto<AdminLoginResponseDto>.Ok(data, "Administrador validado.");
    }
}
