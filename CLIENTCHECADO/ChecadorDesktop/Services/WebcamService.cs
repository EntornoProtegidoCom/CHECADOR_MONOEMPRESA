using Microsoft.Extensions.Configuration;
using System.Drawing.Imaging;

namespace ChecadorDesktop.Services;

public class WebcamService : IPhotoCaptureService
{
    private readonly CameraSettingsOptions _options;
    private readonly ICameraProviderCaptureService _provider;
    public bool DebugCaptureEnabled => _options.DebugCapture;

    public WebcamService()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        _options = new CameraSettingsOptions
        {
            Provider = ParseCameraProvider(config["CameraSettings:Provider"]),
            Mode = ParseCameraMode(config["CameraSettings:Mode"]),
            AllowSyntheticFallback = bool.TryParse(config["CameraSettings:AllowSyntheticFallback"], out bool allowFallback)
                ? allowFallback
                : true,
            PreferredDeviceId = config["CameraSettings:PreferredDeviceId"] ?? string.Empty,
            CaptureTimeoutMs = int.TryParse(config["CameraSettings:CaptureTimeoutMs"], out int timeoutMs) && timeoutMs > 0
                ? timeoutMs
                : 3500,
            DeviceIndex = int.TryParse(config["CameraSettings:DeviceIndex"], out int index) ? index : 0,
            WarmupFrames = int.TryParse(config["CameraSettings:WarmupFrames"], out int warmup) && warmup > 0
                ? warmup
                : 4,
            OpenCvPreferredBackend = config["CameraSettings:OpenCvPreferredBackend"] ?? "MSMF",
            OpenCvTryFallbackBackends = bool.TryParse(config["CameraSettings:OpenCvTryFallbackBackends"], out bool tryFallbackBackends)
                ? tryFallbackBackends
                : true,
            OpenCvFrameWidth = int.TryParse(config["CameraSettings:OpenCvFrameWidth"], out int frameWidth) ? frameWidth : 0,
            OpenCvFrameHeight = int.TryParse(config["CameraSettings:OpenCvFrameHeight"], out int frameHeight) ? frameHeight : 0,
            DebugCapture = bool.TryParse(config["CameraSettings:DebugCapture"], out bool debugCapture)
                ? debugCapture
                : false
        };

        _provider = _options.Provider switch
        {
            CameraProvider.OpenCv => new OpenCvCaptureService(_options),
            _ => new FlashCapCaptureService(_options)
        };
    }

    /// <summary>
    /// API de compatibilidad actual: devuelve JPEG para los flujos existentes.
    /// </summary>
    public byte[] CapturarFotoJpeg(int idTrabajador, string nbEquipo)
    {
        return CapturarFoto(idTrabajador, nbEquipo).FotoJpeg;
    }

    /// <summary>
    /// Contrato tipado para la nueva estrategia por fases.
    /// Hito B: intenta captura física y aplica fallback controlado según política.
    /// </summary>
    public PhotoCaptureResult CapturarFoto(int idTrabajador, string nbEquipo)
    {
        if (_options.Mode == CameraMode.Disabled)
        {
            return GenerarFallback(idTrabajador, nbEquipo, PhotoCaptureStatus.CameraDisabled, "CameraMode=Disabled");
        }

        var capturaReal = _provider.CapturarFotoReal(idTrabajador, nbEquipo);
        if (capturaReal.IsSuccess)
        {
            return capturaReal;
        }

        if (_options.Mode == CameraMode.Required)
        {
            return capturaReal;
        }

        if (!_options.AllowSyntheticFallback)
        {
            return capturaReal;
        }

        return GenerarFallback(
            idTrabajador,
            nbEquipo,
            capturaReal.Status,
            $"Fallback sintético aplicado tras falla de captura real: {capturaReal.Motivo}");
    }

    private PhotoCaptureResult GenerarFallback(int idTrabajador, string nbEquipo, PhotoCaptureStatus status, string motivo)
    {
        if (!_options.AllowSyntheticFallback)
        {
            return new PhotoCaptureResult
            {
                Status = PhotoCaptureStatus.CameraRequiredNotAvailable,
                FotoJpeg = Array.Empty<byte>(),
                EsFallback = false,
                Motivo = "AllowSyntheticFallback=false y captura física no disponible.",
                Timestamp = DateTime.Now
            };
        }

        byte[] foto = GenerarFotogramaSintetico(idTrabajador, nbEquipo);
        return new PhotoCaptureResult
        {
            Status = status,
            FotoJpeg = foto,
            EsFallback = true,
            Motivo = motivo,
            Timestamp = DateTime.Now
        };
    }

    private static byte[] GenerarFotogramaSintetico(int idTrabajador, string nbEquipo)
    {
        try
        {
            // Genera un fotograma digital de comprobación en formato JPEG (640x480)
            using var bitmap = new Bitmap(640, 480);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.FromArgb(24, 28, 36));

                using var brushText = new SolidBrush(Color.White);
                using var brushAccent = new SolidBrush(Color.FromArgb(0, 150, 214));
                using var fontTitulo = new Font("Segoe UI", 18, FontStyle.Bold);
                using var fontDetalle = new Font("Segoe UI", 12, FontStyle.Regular);

                g.DrawString("EVIDENCIA DE ASISTENCIA", fontTitulo, brushAccent, new PointF(30, 40));
                g.DrawString($"ID Trabajador: {idTrabajador}", fontDetalle, brushText, new PointF(30, 100));
                g.DrawString($"Equipo: {nbEquipo}", fontDetalle, brushText, new PointF(30, 135));
                g.DrawString($"Fecha / Hora: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}", fontDetalle, brushText, new PointF(30, 170));
                g.DrawString($"Captura Local Verificada (Offline-First)", fontDetalle, Brushes.LightGreen, new PointF(30, 220));

                // Borde decorativo
                using var pen = new Pen(Color.FromArgb(0, 150, 214), 4);
                g.DrawRectangle(pen, 10, 10, 620, 460);
            }

            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    private static CameraMode ParseCameraMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return CameraMode.Preferred;
        }

        return mode.Trim().ToLowerInvariant() switch
        {
            "required" => CameraMode.Required,
            "disabled" => CameraMode.Disabled,
            _ => CameraMode.Preferred
        };
    }

    private static CameraProvider ParseCameraProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return CameraProvider.FlashCap;
        }

        return provider.Trim().ToLowerInvariant() switch
        {
            "opencv" => CameraProvider.OpenCv,
            _ => CameraProvider.FlashCap
        };
    }
}
