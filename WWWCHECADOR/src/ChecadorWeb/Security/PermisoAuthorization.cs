using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ChecadorWeb.Security;

public sealed class PermisoRequirement(string permiso) : IAuthorizationRequirement
{
    public string Permiso { get; } = permiso;
}

/// <summary>
/// Resuelve los permisos a partir del perfil vigente del usuario (claim validado contra la BD en cada petición).
/// </summary>
public sealed class PermisoAuthorizationHandler(IPermisoResolver resolver) : AuthorizationHandler<PermisoRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermisoRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.TryGetPerfil(out var perfil)
            && resolver.Tiene(perfil, requirement.Permiso))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetPerfil(this ClaimsPrincipal user, out int perfil)
    {
        perfil = 0;
        var valor = user.FindFirstValue(ClaimTypesChecador.Perfil);
        return valor is not null && int.TryParse(valor, out perfil);
    }

    public static string IdUsuario(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No hay usuario autenticado.");
}
