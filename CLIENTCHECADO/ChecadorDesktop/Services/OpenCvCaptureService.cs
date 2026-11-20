using OpenCvSharp;
using System.Diagnostics;

namespace ChecadorDesktop.Services;

public sealed class OpenCvCaptureService : ICameraProviderCaptureService
{
    private readonly int _deviceIndex;
    private readonly int _captureTimeoutMs;
    private readonly int _warmupFrames;
    private readonly string _preferredBackend;
    private readonly bool _tryFallbackBackends;
    private readonly int _frameWidth;
    private readonly int _frameHeight;

    public OpenCvCaptureService(CameraSettingsOptions options)
    {
        _deviceIndex = options.DeviceIndex;
        _captureTimeoutMs = options.CaptureTimeoutMs > 0 ? options.CaptureTimeoutMs : 3500;
        _warmupFrames = options.WarmupFrames > 0 ? options.WarmupFrames : 4;
        _preferredBackend = string.IsNullOrWhiteSpace(options.OpenCvPreferredBackend)
            ? "MSMF"
            : options.OpenCvPreferredBackend.Trim().ToUpperInvariant();
        _tryFallbackBackends = options.OpenCvTryFallbackBackends;
        _frameWidth = options.OpenCvFrameWidth;
        _frameHeight = options.OpenCvFrameHeight;
    }

    public PhotoCaptureResult CapturarFotoReal(int idTrabajador, string nbEquipo)
    {
        var totalSw = Stopwatch.StartNew();
        var backends = ObtenerBackendsEnOrden();
        var errores = new List<string>();

        foreach (var backend in backends)
        {
            try
            {
                using var capture = new VideoCapture(_deviceIndex, backend);
                if (!capture.IsOpened())
                {
                    errores.Add($"{backend}: no se pudo abrir dispositivo index={_deviceIndex}");
                    continue;
                }

                ConfigurarResolucion(capture);

                using var frame = new Mat();
                var sw = Stopwatch.StartNew();

                for (int i = 0; i < _warmupFrames; i++)
                {
                    if (!capture.Read(frame))
                    {
                        break;
                    }
                }

                while (sw.ElapsedMilliseconds <= _captureTimeoutMs)
                {
                    if (capture.Read(frame) && !frame.Empty())
                    {
                        Cv2.ImEncode(".jpg", frame, out var jpegData);
                        if (jpegData.Length > 0)
                        {
                            return new PhotoCaptureResult
                            {
                                Status = PhotoCaptureStatus.Success,
                                FotoJpeg = jpegData,
                                EsFallback = false,
                                Provider = "OpenCv",
                                Backend = backend.ToString(),
                                CaptureDurationMs = totalSw.ElapsedMilliseconds,
                                Motivo = $"Captura física exitosa (OpenCV {backend}) en {sw.ElapsedMilliseconds} ms.",
                                DeviceId = ConstruirDeviceId(backend),
                                Timestamp = DateTime.Now
                            };
                        }

                        errores.Add($"{backend}: frame leído pero codificación JPEG vacía en {sw.ElapsedMilliseconds} ms");
                        break;
                    }
                }

                errores.Add($"{backend}: timeout {_captureTimeoutMs} ms sin frame válido (transcurrido {sw.ElapsedMilliseconds} ms)");
            }
            catch (Exception ex)
            {
                var status = ClasificarExcepcion(ex);
                if (status == PhotoCaptureStatus.PermissionDenied || status == PhotoCaptureStatus.DeviceBusy)
                {
                    return Fallo(status, ex.Message, totalSw.ElapsedMilliseconds, backend.ToString(), ConstruirDeviceId(backend));
                }

                errores.Add($"{backend}: {ex.Message}");
            }
        }

        var motivo = errores.Count > 0
            ? string.Join(" | ", errores)
            : "OpenCV no pudo capturar imagen y no devolvió detalle adicional.";

        // Si hubo intentos pero ninguno obtuvo frame, tratar como timeout operacional.
        return Fallo(PhotoCaptureStatus.Timeout, motivo, totalSw.ElapsedMilliseconds, _preferredBackend, ConstruirDeviceId(null));
    }

    private List<VideoCaptureAPIs> ObtenerBackendsEnOrden()
    {
        var ordered = new List<VideoCaptureAPIs>();
        var preferred = ParseBackend(_preferredBackend);
        if (preferred != VideoCaptureAPIs.ANY)
        {
            ordered.Add(preferred);
        }

        if (_tryFallbackBackends)
        {
            foreach (var candidate in new[] { VideoCaptureAPIs.MSMF, VideoCaptureAPIs.DSHOW, VideoCaptureAPIs.ANY })
            {
                if (!ordered.Contains(candidate))
                {
                    ordered.Add(candidate);
                }
            }
        }

        if (ordered.Count == 0)
        {
            ordered.Add(VideoCaptureAPIs.ANY);
        }

        return ordered;
    }

    private void ConfigurarResolucion(VideoCapture capture)
    {
        if (_frameWidth > 0)
        {
            capture.Set(VideoCaptureProperties.FrameWidth, _frameWidth);
        }

        if (_frameHeight > 0)
        {
            capture.Set(VideoCaptureProperties.FrameHeight, _frameHeight);
        }
    }

    private static VideoCaptureAPIs ParseBackend(string backend)
    {
        return backend switch
        {
            "MSMF" => VideoCaptureAPIs.MSMF,
            "DSHOW" => VideoCaptureAPIs.DSHOW,
            "ANY" => VideoCaptureAPIs.ANY,
            _ => VideoCaptureAPIs.ANY
        };
    }

    private string ConstruirDeviceId(VideoCaptureAPIs? backend)
    {
        return backend.HasValue
            ? $"OpenCV:{backend.Value}:{_deviceIndex}"
            : $"OpenCV:{_deviceIndex}";
    }

    private static PhotoCaptureStatus ClasificarExcepcion(Exception ex)
    {
        if (ex is UnauthorizedAccessException)
        {
            return PhotoCaptureStatus.PermissionDenied;
        }

        const int accessDenied = unchecked((int)0x80070005);
        const int sharingViolation = unchecked((int)0x80070020);

        if (ex.HResult == accessDenied)
        {
            return PhotoCaptureStatus.PermissionDenied;
        }

        if (ex.HResult == sharingViolation)
        {
            return PhotoCaptureStatus.DeviceBusy;
        }

        if (ex is OpenCVException || ex is InvalidOperationException)
        {
            return PhotoCaptureStatus.DriverError;
        }

        return PhotoCaptureStatus.UnexpectedError;
    }

    private static PhotoCaptureResult Fallo(PhotoCaptureStatus status, string motivo, long elapsedMs, string backend, string? deviceId = null)
    {
        return new PhotoCaptureResult
        {
            Status = status,
            FotoJpeg = Array.Empty<byte>(),
            EsFallback = false,
            Provider = "OpenCv",
            Backend = backend,
            CaptureDurationMs = elapsedMs,
            Motivo = motivo,
            DeviceId = deviceId,
            Timestamp = DateTime.Now
        };
    }
}
