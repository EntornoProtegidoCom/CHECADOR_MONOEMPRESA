namespace ChecadorDesktop.Services;

public interface IPhotoCaptureService
{
    PhotoCaptureResult CapturarFoto(int idTrabajador, string nbEquipo);
    byte[] CapturarFotoJpeg(int idTrabajador, string nbEquipo);
}
