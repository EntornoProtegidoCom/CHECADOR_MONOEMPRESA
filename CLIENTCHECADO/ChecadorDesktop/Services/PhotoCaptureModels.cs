namespace ChecadorDesktop.Services;

public enum CameraMode
{
    Preferred,
    Required,
    Disabled
}

public enum PhotoCaptureStatus
{
    Success,
    FallbackUsed,
    CameraDisabled,
    CameraRequiredNotAvailable,
    NoDevice,
    PermissionDenied,
    DeviceBusy,
    DriverError,
    Timeout,
    UnexpectedError
}

public sealed class PhotoCaptureResult
{
    public PhotoCaptureStatus Status { get; init; }
    public byte[] FotoJpeg { get; init; } = Array.Empty<byte>();
    public bool EsFallback { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Backend { get; init; } = string.Empty;
    public long CaptureDurationMs { get; init; }
    public string Motivo { get; init; } = string.Empty;
    public string? DeviceId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public bool IsSuccess => FotoJpeg.Length > 0;
}

public static class PhotoCaptureResultExtensions
{
    public static string ToUiSummary(this PhotoCaptureResult result)
    {
        var baseText = result.EsFallback
            ? $"Fallback ({result.Status})"
            : $"Foto real ({result.Status})";

        string providerPart = string.IsNullOrWhiteSpace(result.Provider)
            ? string.Empty
            : $" [{result.Provider}{(string.IsNullOrWhiteSpace(result.Backend) ? string.Empty : "/" + result.Backend)}]";

        string devicePart = string.IsNullOrWhiteSpace(result.DeviceId)
            ? string.Empty
            : $" - {result.DeviceId}";

        string timingPart = result.CaptureDurationMs > 0
            ? $" ({result.CaptureDurationMs} ms)"
            : string.Empty;

        return $"{baseText}{providerPart}{devicePart}{timingPart}";
    }
}
