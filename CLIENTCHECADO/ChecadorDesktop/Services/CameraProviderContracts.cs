namespace ChecadorDesktop.Services;

public enum CameraProvider
{
    FlashCap,
    OpenCv
}

public sealed class CameraSettingsOptions
{
    public CameraProvider Provider { get; init; } = CameraProvider.FlashCap;
    public CameraMode Mode { get; init; } = CameraMode.Preferred;
    public bool AllowSyntheticFallback { get; init; } = true;
    public string PreferredDeviceId { get; init; } = string.Empty;
    public int CaptureTimeoutMs { get; init; } = 3500;
    public int DeviceIndex { get; init; } = 0;
    public int WarmupFrames { get; init; } = 4;
    public string OpenCvPreferredBackend { get; init; } = "MSMF";
    public bool OpenCvTryFallbackBackends { get; init; } = true;
    public int OpenCvFrameWidth { get; init; } = 0;
    public int OpenCvFrameHeight { get; init; } = 0;
    public bool DebugCapture { get; init; } = false;
}

public interface ICameraProviderCaptureService
{
    PhotoCaptureResult CapturarFotoReal(int idTrabajador, string nbEquipo);
}
