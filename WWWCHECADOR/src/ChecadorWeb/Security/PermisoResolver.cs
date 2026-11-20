using Microsoft.Extensions.Options;

namespace ChecadorWeb.Security;

public interface IPermisoResolver
{
    IReadOnlySet<string> ObtenerPermisos(int idPerfil);
    bool Tiene(int idPerfil, string permiso);
}

public sealed class PermisoResolver(IOptionsMonitor<SeguridadOptions> opciones) : IPermisoResolver
{
    public IReadOnlySet<string> ObtenerPermisos(int idPerfil)
    {
        var resultado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!opciones.CurrentValue.PermisosPorPerfil.TryGetValue(idPerfil.ToString(), out var configurados))
            return resultado;

        foreach (var permiso in configurados)
        {
            if (permiso == Permisos.Comodin)
            {
                resultado.UnionWith(Permisos.Todos);
                continue;
            }
            // Se ignoran permisos desconocidos para no conceder accesos por errores de captura.
            if (Permisos.Todos.Contains(permiso, StringComparer.OrdinalIgnoreCase))
                resultado.Add(permiso);
        }

        foreach (var (editar, ver) in Permisos.Implicaciones)
            if (resultado.Contains(editar)) resultado.Add(ver);

        return resultado;
    }

    public bool Tiene(int idPerfil, string permiso) => ObtenerPermisos(idPerfil).Contains(permiso);
}
