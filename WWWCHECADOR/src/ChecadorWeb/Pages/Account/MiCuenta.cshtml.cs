using ChecadorWeb.Infrastructure;
using ChecadorWeb.Models;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace ChecadorWeb.Pages.Account;

public class MiCuentaModel(IUsuarioService usuarios, IPerfilCatalogo perfiles) : PaginaBase
{
    [BindProperty]
    public CambiarPasswordPropia Datos { get; set; } = new();

    public UsuarioFila? Usuario { get; private set; }
    public string? NombrePerfil { get; private set; }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await CargarAsync(ct);
            return Page();
        }

        var r = await usuarios.CambiarPasswordPropiaAsync(UsuarioActual, Datos.Actual, Datos.Nueva, ct);
        if (!r.Exitoso)
        {
            ModelState.AddModelError(string.Empty, r.Mensaje);
            await CargarAsync(ct);
            return Page();
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        Usuario = await usuarios.ObtenerAsync(UsuarioActual, ct);
        if (Usuario is not null)
            NombrePerfil = (await perfiles.ObtenerAsync(ct)).FirstOrDefault(p => p.Id == Usuario.Perfil)?.Nombre ?? $"Perfil {Usuario.Perfil}";
    }
}
