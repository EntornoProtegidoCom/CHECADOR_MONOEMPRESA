using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using FlashCap;

namespace ChecadorDesktop.Services;

public sealed class FlashCapCaptureService : ICameraProviderCaptureService
{
    private readonly string _preferredDeviceId;
    private readonly int _captureTimeoutMs;

    public FlashCapCaptureService(CameraSettingsOptions options)
    {
        _preferredDeviceId = options.PreferredDeviceId;
        _captureTimeoutMs = options.CaptureTimeoutMs > 0 ? options.CaptureTimeoutMs : 3500;
    }

    public PhotoCaptureResult CapturarFotoReal(int idTrabajador, string nbEquipo)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var devices = new CaptureDevices();
            var descriptors = devices.EnumerateDescriptors().ToArray();
            if (descriptors.Length == 0)
            {
                return Fallo(PhotoCaptureStatus.NoDevice, "No se detectaron cámaras en el equipo.", sw.ElapsedMilliseconds);
            }

            var descriptor = SeleccionarDescriptor(descriptors);
            if (descriptor == null)
            {
                return Fallo(PhotoCaptureStatus.NoDevice, "No se encontró una cámara compatible con la selección configurada.", sw.ElapsedMilliseconds);
            }

            var characteristics = descriptor.Characteristics
                .Where(c => c.PixelFormat != PixelFormats.Unknown)
                .ToArray();

            if (characteristics.Length == 0)
            {
                return Fallo(PhotoCaptureStatus.DriverError, "La cámara no expone formatos compatibles (PixelFormat desconocido).", sw.ElapsedMilliseconds, descriptor.ToString());
            }

            var selectedCharacteristic =
                characteristics.FirstOrDefault(c => c.PixelFormat == PixelFormats.JPEG) ??
                characteristics.FirstOrDefault(c => c.PixelFormat == PixelFormats.PNG) ??
                characteristics[0];

            var timeout = TimeSpan.FromMilliseconds(_captureTimeoutMs);
            var imageData = descriptor
                .TakeOneShotAsync(selectedCharacteristic)
                .WaitAsync(timeout)
                .GetAwaiter()
                .GetResult();

            if (imageData == null || imageData.Length == 0)
            {
                return Fallo(PhotoCaptureStatus.DriverError, "La cámara devolvió una imagen vacía.", sw.ElapsedMilliseconds, descriptor.ToString());
            }

            var jpegData = AsegurarJpeg(imageData);
            if (jpegData.Length == 0)
            {
                return Fallo(PhotoCaptureStatus.DriverError, "No se pudo convertir la imagen capturada a JPEG.", sw.ElapsedMilliseconds, descriptor.ToString());
            }

            return new PhotoCaptureResult
            {
                Status = PhotoCaptureStatus.Success,
                FotoJpeg = jpegData,
                EsFallback = false,
                Provider = "FlashCap",
                Backend = "DirectShow/MediaFoundation",
                CaptureDurationMs = sw.ElapsedMilliseconds,
                Motivo = "Captura física exitosa.",
                DeviceId = descriptor.ToString(),
                Timestamp = DateTime.Now
            };
        }
        catch (TimeoutException)
        {
            return Fallo(PhotoCaptureStatus.Timeout, $"La cámara no respondió dentro del timeout ({_captureTimeoutMs} ms).", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            var status = ClasificarExcepcion(ex);
            return Fallo(status, ex.Message, sw.ElapsedMilliseconds);
        }
    }

    private CaptureDeviceDescriptor? SeleccionarDescriptor(CaptureDeviceDescriptor[] descriptors)
    {
        if (descriptors.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_preferredDeviceId))
        {
            var selected = descriptors.FirstOrDefault(d =>
                d.ToString().Contains(_preferredDeviceId, StringComparison.OrdinalIgnoreCase));
            if (selected != null)
            {
                return selected;
            }
        }

        return descriptors[0];
    }

    private static byte[] AsegurarJpeg(byte[] imageData)
    {
        try
        {
            using var ms = new MemoryStream(imageData);
            using var image = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: false);
            using var outMs = new MemoryStream();
            image.Save(outMs, ImageFormat.Jpeg);
            return outMs.ToArray();
        }
        catch
        {
            return Array.Empty<byte>();
        }
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

        if (ex is InvalidOperationException)
        {
            return PhotoCaptureStatus.DriverError;
        }

        return PhotoCaptureStatus.UnexpectedError;
    }

    private static PhotoCaptureResult Fallo(PhotoCaptureStatus status, string motivo, long elapsedMs, string? deviceId = null)
    {
        return new PhotoCaptureResult
        {
            Status = status,
            FotoJpeg = Array.Empty<byte>(),
            EsFallback = false,
            Provider = "FlashCap",
            Backend = "DirectShow/MediaFoundation",
            CaptureDurationMs = elapsedMs,
            Motivo = motivo,
            DeviceId = deviceId,
            Timestamp = DateTime.Now
        };
    }
}
