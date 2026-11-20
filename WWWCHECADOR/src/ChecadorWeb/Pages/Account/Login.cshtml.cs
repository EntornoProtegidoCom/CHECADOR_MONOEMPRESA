using System.ComponentModel.DataAnnotations;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace ChecadorWeb.Pages.Account;

[EnableRateLimiting("login")]
public class LoginModel(IAutenticacionService autenticacion, IAuditoria auditoria, ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public Entrada Datos { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    public sealed class Entrada
    {
        [Required(ErrorMessage = "Capture su usuario.")]
        [StringLength(25)]
        [Display(Name = "Usuario")]
        public string Usuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "Capture su contraseña.")]
        [StringLength(LegacyPasswordHasher.LongitudMaxima)]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(DestinoSeguro());
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();

        LoginResultado resultado;
        try
        {
            resultado = await autenticacion.ValidarAsync(Datos.Usuario, Datos.Password, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error al validar credenciales.");
            Error = "No fue posible validar sus credenciales en este momento. Intente más tarde.";
            return Page();
        }

        var usuario = Datos.Usuario.Trim();
        if (resultado.Resultado != ResultadoLogin.Exito)
        {
            await auditoria.RegistrarAsync(new EventoAuditoria("LOGIN", "SS_USUARIOS", usuario, false, resultado.Resultado.ToString()), ct);
            Error = resultado.Resultado == ResultadoLogin.SinPermisos
                ? "Su perfil no tiene acceso a este sitio."
                : "Usuario o contraseña incorrectos, o usuario inactivo.";
            Datos.Password = string.Empty;
            return Page();
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, resultado.Principal!,
            new AuthenticationProperties { IsPersistent = false });
        HttpContext.User = resultado.Principal!;
        await auditoria.RegistrarAsync(new EventoAuditoria("LOGIN", "SS_USUARIOS", usuario, true), ct);
        return LocalRedirect(DestinoSeguro());
    }

    private string DestinoSeguro() =>
        !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Content("~/");
}
