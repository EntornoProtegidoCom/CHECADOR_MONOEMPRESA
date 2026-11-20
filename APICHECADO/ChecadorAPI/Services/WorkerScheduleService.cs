using System.Data;
using System.Data.Common;
using System.Globalization;
using ChecadorAPI.Data;
using ChecadorAPI.Models.DTOs;

namespace ChecadorAPI.Services;

public sealed class WorkerScheduleService(ISqlConnectionFactory dbFactory) : IWorkerScheduleService
{
    public async Task<ApiResponseDto<WorkerScheduleResponseDto>> GetScheduleAsync(int idTrabajador, CancellationToken cancellationToken)
    {
        await using var connection = (DbConnection)dbFactory.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT IDTrabajador, Nombre, ApellidoP, ApellidoM, status,
                   HoraEntrada, HoraSalidaComida, HoraEntradaComida, HoraSalida
            FROM dbo.MM_TRABAJADORES
            WHERE IDTrabajador = @idTrabajador
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@idTrabajador";
        parameter.DbType = DbType.Int32;
        parameter.Value = idTrabajador;
        command.Parameters.Add(parameter);

        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader["status"] is DBNull || !Convert.ToBoolean(reader["status"]))
        {
            return ApiResponseDto<WorkerScheduleResponseDto>.Warning("El trabajador no existe o esta inactivo.");
        }

        var horaEntrada = FormatHour(reader["HoraEntrada"]);
        var horaSalidaComida = FormatHour(reader["HoraSalidaComida"]);
        var horaEntradaComida = FormatHour(reader["HoraEntradaComida"]);
        var horaSalida = FormatHour(reader["HoraSalida"]);
        if (horaEntrada is null || horaSalidaComida is null || horaEntradaComida is null || horaSalida is null)
        {
            return ApiResponseDto<WorkerScheduleResponseDto>.Warning("El trabajador no tiene horarios completos configurados.");
        }

        var nombreCompleto = string.Join(" ", new[] { reader["Nombre"], reader["ApellidoP"], reader["ApellidoM"] }
            .Select(value => Convert.ToString(value)?.Trim())
            .Where(value => !string.IsNullOrEmpty(value)));
        var data = new WorkerScheduleResponseDto(idTrabajador, nombreCompleto,
            horaEntrada, horaSalidaComida, horaEntradaComida, horaSalida);
        return ApiResponseDto<WorkerScheduleResponseDto>.Ok(data, "Horarios obtenidos correctamente");
    }

    private static string? FormatHour(object value)
    {
        TimeOnly hour;
        switch (value)
        {
            case TimeSpan time when time >= TimeSpan.Zero && time < TimeSpan.FromDays(1):
                hour = TimeOnly.FromTimeSpan(time);
                break;
            case DateTime date:
                hour = TimeOnly.FromDateTime(date);
                break;
            case TimeOnly time:
                hour = time;
                break;
            case string text when TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed):
                hour = parsed;
                break;
            default:
                return null;
        }

        return hour.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}