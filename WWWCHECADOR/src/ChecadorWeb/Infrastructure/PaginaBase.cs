using System.Text.Json;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChecadorWeb.Infrastructure;

/// <summary>Utilidades comunes: usuario actual, permisos y mensajes de confirmación entre redirecciones.</summary>
public abstract class PaginaBase : PageModel
{
    [TempData]
    public string? MensajeSerializado { get; set; }

    public ResultadoOperacion? Mensaje =>
        MensajeSerializado is null ? null : JsonSerializer.Deserialize<ResultadoOperacion>(MensajeSerializado);

    protected string UsuarioActual => User.IdUsuario();

    protected void Notificar(ResultadoOperacion resultado) =>
        MensajeSerializado = JsonSerializer.Serialize(resultado);

    public bool Puede(string permiso)
    {
        var resolver = HttpContext.RequestServices.GetRequiredService<IPermisoResolver>();
        return User.TryGetPerfil(out var perfil) && resolver.Tiene(perfil, permiso);
    }

    /// <summary>
    /// Razor Pages no admite [Authorize] por handler; los handlers que modifican datos validan su permiso aquí.
    /// </summary>
    protected async Task<bool> AutorizarAsync(string permiso)
    {
        var auth = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        return (await auth.AuthorizeAsync(User, permiso)).Succeeded;
    }

    protected void AgregarErrores(string prefijo, IEnumerable<Services.ErrorHorario> errores)
    {
        foreach (var e in errores) ModelState.AddModelError($"{prefijo}.{e.Campo}", e.Mensaje);
    }

    /// <summary>Mensaje mostrado cuando la consulta principal de la página falla.</summary>
    public string? ErrorCarga { get; protected set; }

    protected async Task<T?> CargarAsync<T>(Func<Task<T>> consulta) where T : class?
    {
        try
        {
            return await consulta();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<PaginaBase>>()
                .LogError(ex, "Error al consultar datos para {Pagina}.", HttpContext.Request.Path.Value);
            ErrorCarga = "No fue posible consultar la información. Verifique la conexión e intente de nuevo.";
            return null;
        }
    }

    /// <summary>Ejecuta una operación de escritura y convierte errores inesperados en un mensaje seguro.</summary>
    protected async Task<ResultadoOperacion> EjecutarAsync(Func<Task<ResultadoOperacion>> operacion)
    {
        try
        {
            return await operacion();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<PaginaBase>>()
                .LogError(ex, "Error al ejecutar una operación en {Pagina}.", HttpContext.Request.Path.Value);
            return ResultadoOperacion.Fallo("No fue posible completar la operación. Verifique el estado del registro e intente de nuevo.");
        }
    }

    /// <summary>Redirige a la lista conservando los filtros, aceptando solo una query string local.</summary>
    protected IActionResult VolverALista(string? volver, string pagina = "./Index")
    {
        var destino = Url.Page(pagina) ?? "/";
        if (!string.IsNullOrEmpty(volver) && volver.StartsWith('?') && !volver.Contains("//"))
            destino += volver;
        return LocalRedirect(destino);
    }
}
