namespace ChecadorWeb.Security;

public sealed class SeguridadOptions
{
    public const string Seccion = "Seguridad";

    /// <summary>ID_PERFIL (como texto) → lista de permisos. "*" concede todos.</summary>
    public Dictionary<string, string[]> PermisosPorPerfil { get; set; } = new();

    /// <summary>Nombres de perfil usados mientras no exista dbo.SS_PERFIL.</summary>
    public Dictionary<string, string> NombresPerfil { get; set; } = new();

    /// <summary>Valor de SS_USUARIOS.ST_VIGENTE para usuarios activos (validado por los SP existentes).</summary>
    public string ValorVigente { get; set; } = "A";

    /// <summary>Valor de SS_USUARIOS.ST_VIGENTE usado al desactivar.</summary>
    public string ValorInactivo { get; set; } = "I";

    public int MinutosSesion { get; set; } = 30;

    public int IntentosLoginPorMinuto { get; set; } = 10;
}
