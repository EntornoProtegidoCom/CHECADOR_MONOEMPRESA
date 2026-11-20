namespace ChecadorWeb.Security;

/// <summary>
/// Permisos del sitio. Cada permiso se registra como política de autorización con el mismo nombre.
/// La asignación perfil → permisos se configura en <c>Seguridad:PermisosPorPerfil</c>.
/// </summary>
public static class Permisos
{
    public const string Comodin = "*";

    public const string PanelVer = "panel.ver";
    public const string EmpleadosVer = "empleados.ver";
    public const string EmpleadosEditar = "empleados.editar";
    public const string HorariosVer = "horarios.ver";
    public const string HorariosEditar = "horarios.editar";
    public const string EquiposVer = "equipos.ver";
    public const string EquiposEditar = "equipos.editar";
    public const string UsuariosVer = "usuarios.ver";
    public const string UsuariosEditar = "usuarios.editar";
    public const string EstacionesVer = "estaciones.ver";
    public const string MarcajesVer = "marcajes.ver";
    public const string MarcajesFoto = "marcajes.foto";
    public const string DiasFestivosVer = "diasfestivos.ver";
    public const string DiasFestivosEditar = "diasfestivos.editar";

    public static readonly IReadOnlyList<string> Todos =
    [
        PanelVer, EmpleadosVer, EmpleadosEditar, HorariosVer, HorariosEditar,
        EquiposVer, EquiposEditar, UsuariosVer, UsuariosEditar, EstacionesVer,
        MarcajesVer, MarcajesFoto, DiasFestivosVer, DiasFestivosEditar
    ];

    /// <summary>Un permiso de edición implica el de consulta del mismo módulo.</summary>
    public static readonly IReadOnlyDictionary<string, string> Implicaciones = new Dictionary<string, string>
    {
        [EmpleadosEditar] = EmpleadosVer,
        [HorariosEditar] = HorariosVer,
        [EquiposEditar] = EquiposVer,
        [UsuariosEditar] = UsuariosVer,
        [MarcajesFoto] = MarcajesVer,
        [DiasFestivosEditar] = DiasFestivosVer,
    };
}

public static class ClaimTypesChecador
{
    public const string Perfil = "checador:perfil";
    public const string SelloCredencial = "checador:sello";
}
