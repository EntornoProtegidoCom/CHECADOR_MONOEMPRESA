using System.Security.Claims;
using ChecadorWeb.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChecadorWeb.Security;

public enum ResultadoLogin { Exito, CredencialesInvalidas, SinPermisos }

public sealed record LoginResultado(ResultadoLogin Resultado, ClaimsPrincipal? Principal = null);

public interface IAutenticacionService
{
    Task<LoginResultado> ValidarAsync(string idUsuario, string password, CancellationToken ct = default);
}

public sealed class AutenticacionService(
    ChecadorDbContext db,
    IPermisoResolver permisos,
    IOptionsMonitor<SeguridadOptions> opciones) : IAutenticacionService
{
    // Se calcula un hash aunque el usuario no exista para no revelar su existencia por tiempo de respuesta.
    private static readonly string HashFicticio = LegacyPasswordHasher.Hash(Guid.NewGuid().ToString());

    public async Task<LoginResultado> ValidarAsync(string idUsuario, string password, CancellationToken ct = default)
    {
        var id = idUsuario.Trim();
        var usuario = await db.SS_USUARIOs.AsNoTracking()
            .Where(u => u.ID_USUARIO == id)
            .Select(u => new { u.ID_USUARIO, u.ID_PERFIL, u.NB_NOMBRE, u.NB_PATERNO, u.NB_MATERNO, u.TX_PASSWORD, u.ST_VIGENTE })
            .SingleOrDefaultAsync(ct);

        var passwordValida = LegacyPasswordHasher.Verify(password, usuario?.TX_PASSWORD ?? HashFicticio);
        if (usuario is null || !passwordValida || usuario.ST_VIGENTE != opciones.CurrentValue.ValorVigente)
            return new LoginResultado(ResultadoLogin.CredencialesInvalidas);

        if (permisos.ObtenerPermisos(usuario.ID_PERFIL).Count == 0)
            return new LoginResultado(ResultadoLogin.SinPermisos);

        var nombre = string.Join(' ', new[] { usuario.NB_NOMBRE, usuario.NB_PATERNO, usuario.NB_MATERNO }
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));

        var identidad = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, usuario.ID_USUARIO),
            new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(nombre) ? usuario.ID_USUARIO : nombre),
            new Claim(ClaimTypesChecador.Perfil, usuario.ID_PERFIL.ToString()),
            new Claim(ClaimTypesChecador.SelloCredencial, LegacyPasswordHasher.Sello(usuario.TX_PASSWORD)),
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        return new LoginResultado(ResultadoLogin.Exito, new ClaimsPrincipal(identidad));
    }
}

/// <summary>
/// Revalida la sesión en cada petición: el usuario debe seguir vigente, conservar su perfil
/// y no haber cambiado su contraseña. Así, desactivar un usuario corta su acceso de inmediato.
/// </summary>
public sealed class ValidadorSesionCookie(IOptionsMonitor<SeguridadOptions> opciones, ILogger<ValidadorSesionCookie> logger)
    : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var id = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (principal is null || id is null || !principal.TryGetPerfil(out var perfil))
        {
            await Rechazar(context);
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<ChecadorDbContext>();
        var actual = await db.SS_USUARIOs.AsNoTracking()
            .Where(u => u.ID_USUARIO == id)
            .Select(u => new { u.ID_PERFIL, u.ST_VIGENTE, u.TX_PASSWORD })
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        if (actual is null
            || actual.ST_VIGENTE != opciones.CurrentValue.ValorVigente
            || actual.ID_PERFIL != perfil
            || LegacyPasswordHasher.Sello(actual.TX_PASSWORD) != principal.FindFirstValue(ClaimTypesChecador.SelloCredencial))
        {
            logger.LogInformation("Sesión invalidada para {Usuario}: cambió su estado, perfil o credencial.", id);
            await Rechazar(context);
        }
    }

    private static async Task Rechazar(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
