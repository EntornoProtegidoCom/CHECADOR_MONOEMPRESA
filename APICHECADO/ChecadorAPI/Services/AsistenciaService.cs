using System.Data;
using Microsoft.Data.SqlClient;
using ChecadorAPI.Data;
using ChecadorAPI.Models.DTOs;

namespace ChecadorAPI.Services;

public class AsistenciaService : IAsistenciaService
{
    private readonly ISqlConnectionFactory _dbFactory;
    private readonly bool _needPin;
    private readonly ILogger<AsistenciaService> _logger;

    public AsistenciaService(
        ISqlConnectionFactory dbFactory,
        IConfiguration configuration,
        ILogger<AsistenciaService> logger)
    {
        _dbFactory = dbFactory;
        _needPin = configuration.GetValue<bool?>("ApiSettings:NeedPin") ?? true;
        _logger = logger;
    }

    public async Task<ApiResponseDto<MarcajeResponseDto>> RegistrarMarcajeAsync(MarcajeRequestDto req)
    {
        var authMode = GetAuthMode(req.Pin);
        var validationError = ValidateMarcajeRequest(req, requireFechaHoraCaptura: false);
        if (!string.IsNullOrEmpty(validationError))
        {
            _logger.LogWarning(
                "Marcaje online rechazado por validacion. NeedPin={NeedPin}, Modo={Modo}, IdTrabajador={IdTrabajador}, NbEquipo={NbEquipo}, TipoRegistro={TipoRegistro}, Error={Error}",
                _needPin,
                authMode,
                req.IdTrabajador,
                req.NbEquipo,
                req.TipoRegistro,
                validationError);
            return ApiResponseDto<MarcajeResponseDto>.Warning(validationError);
        }

        _logger.LogInformation(
            "Marcaje online recibido. NeedPin={NeedPin}, Modo={Modo}, IdTrabajador={IdTrabajador}, NbEquipo={NbEquipo}, TipoRegistro={TipoRegistro}",
            _needPin,
            authMode,
            req.IdTrabajador,
            req.NbEquipo,
            req.TipoRegistro);

        var pinForPayload = req.Pin.HasValue && req.Pin.Value > 0 ? req.Pin : null;

        var xml = XmlPayloadBuilder.BuildXml("I", new()
        {
            ["IDTrabajador"] = req.IdTrabajador,
            ["PIN"] = pinForPayload,
            ["TipoRegistro"] = req.TipoRegistro,
            ["IPRegistro"] = req.IpRegistro,
            ["NBEquipo"] = req.NbEquipo
        });

        byte[]? fotoBytes;
        try
        {
            fotoBytes = string.IsNullOrWhiteSpace(req.FotoBase64)
                ? null
                : Convert.FromBase64String(req.FotoBase64);
        }
        catch (FormatException)
        {
            _logger.LogWarning(
                "Marcaje online rechazado por FotoBase64 invalida. IdTrabajador={IdTrabajador}, NbEquipo={NbEquipo}",
                req.IdTrabajador,
                req.NbEquipo);
            return ApiResponseDto<MarcajeResponseDto>.Warning("FotoBase64 no tiene un formato valido.");
        }

        using var conn = (SqlConnection)_dbFactory.CreateConnection();
        using var cmd = new SqlCommand("sp_registro_checador", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add(new SqlParameter("@param", SqlDbType.NText) { Value = xml });

        var pFoto = cmd.Parameters.Add("@img_data", SqlDbType.Image);
        pFoto.Value = (object?)fotoBytes ?? DBNull.Value;

        await conn.OpenAsync();
        using var adapter = new SqlDataAdapter(cmd);
        var ds = new DataSet();
        adapter.Fill(ds);

        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
        {
            _logger.LogError(
                "Marcaje online sin respuesta del SP. IdTrabajador={IdTrabajador}, NbEquipo={NbEquipo}, TipoRegistro={TipoRegistro}",
                req.IdTrabajador,
                req.NbEquipo,
                req.TipoRegistro);
            return ApiResponseDto<MarcajeResponseDto>.Error("Sin respuesta del procedimiento almacenado.");
        }

        var statusRow = ds.Tables[0].Rows[0];
        int exito = Convert.ToInt32(statusRow["EXITO"]);
        string mensaje = statusRow["MENSAJE"]?.ToString() ?? string.Empty;

        if (exito != 1)
        {
            _logger.LogWarning(
                "Marcaje online no exitoso. Exito={Exito}, IdTrabajador={IdTrabajador}, NbEquipo={NbEquipo}, TipoRegistro={TipoRegistro}, Mensaje={Mensaje}",
                exito,
                req.IdTrabajador,
                req.NbEquipo,
                req.TipoRegistro,
                mensaje);
            return ApiResponseDto<MarcajeResponseDto>.Warning(mensaje);
        }

        var data = new MarcajeResponseDto();
        if (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0)
        {
            var row = ds.Tables[1].Rows[0];
            data.IdTrabajador = req.IdTrabajador;
            data.NombreCompleto = row["NombreCompleto"]?.ToString() ?? string.Empty;
            data.Correo = row["Correo"]?.ToString() ?? string.Empty;
            data.Horaregistro = row["Horaregistro"]?.ToString() ?? string.Empty;
            data.TipoRegistro = row["TipoRegistro"]?.ToString() ?? string.Empty;
            if (row["rHash"] != DBNull.Value)
            {
                data.HashRegistroHex = Convert.ToHexString((byte[])row["rHash"]);
            }
        }

        _logger.LogInformation(
            "Marcaje online exitoso. IdTrabajador={IdTrabajador}, NbEquipo={NbEquipo}, TipoRegistro={TipoRegistro}, Mensaje={Mensaje}",
            req.IdTrabajador,
            req.NbEquipo,
            req.TipoRegistro,
            mensaje);

        return ApiResponseDto<MarcajeResponseDto>.Ok(data, mensaje);
    }

    public async Task<ApiResponseDto<MarcajeBatchResultDto>> ProcesarLoteOfflineAsync(MarcajeBatchRequestDto batch)
    {
        _logger.LogInformation("Inicio de lote offline. Registros={TotalRegistros}", batch.Registros.Count);

        var resultado = new MarcajeBatchResultDto { TotalProcesados = batch.Registros.Count };

        foreach (var reg in batch.Registros.OrderBy(r => r.FechaHoraCaptura))
        {
            var res = await RegistrarMarcajeOfflineAsync(reg);
            if (res.Exito == 1)
            {
                resultado.Exitosos++;
                resultado.Mensajes.Add($"GUID {reg.GuidRegistro}: {res.Mensaje}");
            }
            else
            {
                resultado.Fallidos++;
                resultado.Mensajes.Add($"GUID {reg.GuidRegistro} [Error]: {res.Mensaje}");
            }
        }

        _logger.LogInformation(
            "Fin de lote offline. Total={TotalProcesados}, Exitosos={Exitosos}, Fallidos={Fallidos}",
            resultado.TotalProcesados,
            resultado.Exitosos,
            resultado.Fallidos);

        return ApiResponseDto<MarcajeBatchResultDto>.Ok(resultado, $"Lote procesado: {resultado.Exitosos} exitosos, {resultado.Fallidos} fallidos.");
    }

    private async Task<ApiResponseDto<MarcajeResponseDto>> RegistrarMarcajeOfflineAsync(MarcajeRequestDto req)
    {
        var authMode = GetAuthMode(req.Pin);
        var validationError = ValidateMarcajeRequest(req, requireFechaHoraCaptura: true);
        if (!string.IsNullOrEmpty(validationError))
        {
            _logger.LogWarning(
                "Marcaje offline rechazado por validacion. NeedPin={NeedPin}, Modo={Modo}, IdTrabajador={IdTrabajador}, NbEquipo={NbEquipo}, TipoRegistro={TipoRegistro}, FechaHoraCaptura={FechaHoraCaptura}, Error={Error}",
                _needPin,
                authMode,
                req.IdTrabajador,
                req.NbEquipo,
                req.TipoRegistro,
                req.FechaHoraCaptura,
                validationError);
            return ApiResponseDto<MarcajeResponseDto>.Warning(validationError);
        }

        _logger.LogInformation(
            "Marcaje offline recibido. NeedPin={NeedPin}, Modo={Modo}, IdTrabajador={IdTrabajador}, NbEquipo={NbEquipo}, TipoRegistro={TipoRegistro}, FechaHoraCaptura={FechaHoraCaptura}",
            _needPin,
            authMode,
            req.IdTrabajador,
            req.NbEquipo,
            req.TipoRegistro,
            req.FechaHoraCaptura);

        var pinForPayload = req.Pin.HasValue && req.Pin.Value > 0 ? req.Pin : null;

        var xml = XmlPayloadBuilder.BuildXml("O", new()
        {
            ["IDTrabajador"] = req.IdTrabajador,
            ["PIN"] = pinForPayload,
            ["TipoRegistro"] = req.TipoRegistro,
            ["Horaregistro"] = req.FechaHoraCaptura.ToString("yyyy-MM-ddTHH:mm:ss"),
            ["IPRegistro"] = req.IpRegistro,
            ["NBEquipo"] = req.NbEquipo
        });

        byte[]? fotoBytes;
        try
        {
            fotoBytes = string.IsNullOrWhiteSpace(req.FotoBase64)
                ? null
                : Convert.FromBase64String(req.FotoBase64);
        }
        catch (FormatException)
        {
            _logger.LogWarning(
                "Marcaje offline rechazado por FotoBase64 invalida. IdTrabajador={IdTrabajador}, NbEquipo={NbEquipo}",
                req.IdTrabajador,
                req.NbEquipo);
            return ApiResponseDto<MarcajeResponseDto>.Warning("FotoBase64 no tiene un formato valido.");
        }

        using var conn = (SqlConnection)_dbFactory.CreateConnection();
        using var cmd = new SqlCommand("sp_registro_checador", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add(new SqlParameter("@param", SqlDbType.NText) { Value = xml });

        var pFoto = cmd.Parameters.Add("@img_data", SqlDbType.Image);
        pFoto.Value = (object?)fotoBytes ?? DBNull.Value;

        await conn.OpenAsync();
        using var adapter = new SqlDataAdapter(cmd);
        var ds = new DataSet();
        adapter.Fill(ds);

        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
        {
            _logger.LogError(
                "Marcaje offline sin respuesta del SP. IdTrabajador={IdTrabajador}, NbEquipo={NbEquipo}, TipoRegistro={TipoRegistro}, FechaHoraCaptura={FechaHoraCaptura}",
                req.IdTrabajador,
                req.NbEquipo,
                req.TipoRegistro,
                req.FechaHoraCaptura);
            return ApiResponseDto<MarcajeResponseDto>.Error("Sin respuesta del procedimiento almacenado.");
        }

        var statusRow = ds.Tables[0].Rows[0];
        int exito = Convert.ToInt32(statusRow["EXITO"]);
        string mensaje = statusRow["MENSAJE"]?.ToString() ?? string.Empty;

        if (exito != 1)
        {
            _logger.LogWarning(
                "Marcaje offline no exitoso. Exito={Exito}, IdTrabajador={IdTrabajador}, NbEquipo={NbEquipo}, TipoRegistro={TipoRegistro}, FechaHoraCaptura={FechaHoraCaptura}, Mensaje={Mensaje}",
                exito,
                req.IdTrabajador,
                req.NbEquipo,
                req.TipoRegistro,
                req.FechaHoraCaptura,
                mensaje);
            return ApiResponseDto<MarcajeResponseDto>.Warning(mensaje);
        }

        var data = new MarcajeResponseDto();
        if (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0)
        {
            var row = ds.Tables[1].Rows[0];
            data.IdTrabajador = req.IdTrabajador;
            data.NombreCompleto = row["NombreCompleto"]?.ToString() ?? string.Empty;
            data.Correo = row["Correo"]?.ToString() ?? string.Empty;
            data.Horaregistro = row["Horaregistro"]?.ToString() ?? string.Empty;
            data.TipoRegistro = row["TipoRegistro"]?.ToString() ?? string.Empty;
            if (row["rHash"] != DBNull.Value)
            {
                data.HashRegistroHex = Convert.ToHexString((byte[])row["rHash"]);
            }
        }

        _logger.LogInformation(
            "Marcaje offline exitoso. IdTrabajador={IdTrabajador}, NbEquipo={NbEquipo}, TipoRegistro={TipoRegistro}, FechaHoraCaptura={FechaHoraCaptura}, Mensaje={Mensaje}",
            req.IdTrabajador,
            req.NbEquipo,
            req.TipoRegistro,
            req.FechaHoraCaptura,
            mensaje);

        return ApiResponseDto<MarcajeResponseDto>.Ok(data, mensaje);
    }

    private static string GetAuthMode(int? pin) => pin.HasValue && pin.Value > 0 ? "con-pin" : "sin-pin";

    private string? ValidateMarcajeRequest(MarcajeRequestDto req, bool requireFechaHoraCaptura)
    {
        if (req.IdTrabajador <= 0)
            return "El ID del trabajador debe ser mayor a 0.";

        if (_needPin && (!req.Pin.HasValue || req.Pin.Value <= 0))
            return "El PIN es obligatorio y debe ser mayor a 0 cuando NeedPin está activo.";

        if (req.TipoRegistro is < 1 or > 4)
            return "TipoRegistro invalido. Valores permitidos: 1, 2, 3, 4.";

        if (requireFechaHoraCaptura && req.FechaHoraCaptura == default)
            return "FechaHoraCaptura es obligatoria para el flujo offline.";

        if (string.IsNullOrWhiteSpace(req.IpRegistro))
            return "IPRegistro es obligatorio.";

        if (string.IsNullOrWhiteSpace(req.NbEquipo))
            return "NBEquipo es obligatorio.";

        return null;
    }
}
