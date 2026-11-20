using System.ComponentModel.DataAnnotations;
using ChecadorWeb.Models;
using ChecadorWeb.Security;

namespace ChecadorWeb.Tests.Unit;

public class PermisoResolverTests
{
    private static PermisoResolver Crear(Dictionary<string, string[]> mapa) =>
        new(new MonitorFijo<SeguridadOptions>(new SeguridadOptions { PermisosPorPerfil = mapa }));

    [Fact]
    public void Comodin_concede_todos_los_permisos()
    {
        var r = Crear(new() { ["1"] = ["*"] });
        Assert.Equal(Permisos.Todos.Count, r.ObtenerPermisos(1).Count);
    }

    [Fact]
    public void Perfil_no_configurado_no_tiene_permisos()
    {
        var r = Crear(new() { ["1"] = ["*"] });
        Assert.Empty(r.ObtenerPermisos(2));
        Assert.False(r.Tiene(2, Permisos.PanelVer));
    }

    [Fact]
    public void Editar_implica_ver_pero_no_al_reves()
    {
        var r = Crear(new() { ["2"] = [Permisos.EmpleadosEditar], ["3"] = [Permisos.EmpleadosVer] });
        Assert.True(r.Tiene(2, Permisos.EmpleadosVer));
        Assert.False(r.Tiene(3, Permisos.EmpleadosEditar));
    }

    [Fact]
    public void Permisos_desconocidos_se_ignoran()
    {
        var r = Crear(new() { ["2"] = ["empleados.borrar", "admin", Permisos.EstacionesVer] });
        Assert.Equal([Permisos.EstacionesVer], r.ObtenerPermisos(2));
    }

    [Fact]
    public void Foto_de_marcaje_implica_consulta_de_marcajes()
    {
        var r = Crear(new() { ["4"] = [Permisos.MarcajesFoto] });
        Assert.True(r.Tiene(4, Permisos.MarcajesVer));
    }
}

public class ValidacionModelosTests
{
    private static List<ValidationResult> Validar(object modelo)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), resultados, validateAllProperties: true);
        return resultados;
    }

    private static EmpleadoAlta Empleado(string pin, string confirmar) => new()
    {
        Nombre = "ANA", ApellidoP = "PEREZ", ApellidoM = "LOPEZ", Pin = pin, ConfirmarPin = confirmar,
        IDTrabajador = 1,
    };

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(123, true)]
    public void ID_de_trabajador_debe_ser_positivo(int? id, bool valido)
    {
        var empleado = Empleado("1234", "1234");
        empleado.IDTrabajador = id;

        Assert.Equal(valido, Validar(empleado).Count == 0);
    }

    [Theory]
    [InlineData("1234", true)]
    [InlineData("9999", true)]
    [InlineData("0123", false)]
    [InlineData("123", false)]
    [InlineData("12345", false)]
    [InlineData("12a4", false)]
    public void Pin_debe_ser_de_cuatro_digitos_compatibles_con_Contrasena(string pin, bool valido)
    {
        Assert.Equal(valido, Validar(Empleado(pin, pin)).Count == 0);
    }

    [Fact]
    public void Pin_y_confirmacion_deben_coincidir()
    {
        Assert.NotEmpty(Validar(Empleado("1234", "4321")));
    }

    [Fact]
    public void Nombres_respetan_longitud_de_columna()
    {
        var e = Empleado("1234", "1234");
        e.Nombre = new string('A', 21);
        Assert.Contains(Validar(e), r => r.MemberNames.Contains(nameof(EmpleadoAlta.Nombre)));
    }

    [Theory]
    [InlineData("Segura123", true)]
    [InlineData("corta1", false)]
    [InlineData("SoloLetrasSinNumero", false)]
    [InlineData("12345678", false)]
    [InlineData(" Espacio123", false)]
    public void Politica_de_contrasena(string password, bool valida)
    {
        var u = new UsuarioAlta { IdUsuario = "qa.usuario", IdPerfil = 1, Nombre = "QA", Password = password, ConfirmarPassword = password };
        Assert.Equal(valida, Validar(u).Count == 0);
    }

    [Fact]
    public void Contrasena_no_supera_limite_de_sp_seguridad_usuarios()
    {
        var p = "A1" + new string('x', LegacyPasswordHasher.LongitudMaxima - 1);
        var u = new UsuarioAlta { IdUsuario = "qa.usuario", IdPerfil = 1, Nombre = "QA", Password = p, ConfirmarPassword = p };
        Assert.NotEmpty(Validar(u));
    }

    [Theory]
    [InlineData("qa.usuario", true)]
    [InlineData("ab", false)]
    [InlineData("usuario con espacio", false)]
    [InlineData("usuario<script>", false)]
    public void Identificador_de_usuario(string id, bool valido)
    {
        var u = new UsuarioAlta { IdUsuario = id, IdPerfil = 1, Nombre = "QA", Password = "Segura123", ConfirmarPassword = "Segura123" };
        Assert.Equal(valido, Validar(u).Count == 0);
    }

    [Theory]
    [InlineData("PC-RECEPCION-01", true)]
    [InlineData("LPENTORNO165", true)]
    [InlineData("-INVALIDO", false)]
    [InlineData("EQUIPO CON ESPACIO", false)]
    public void Nombre_de_equipo(string equipo, bool valido)
    {
        Assert.Equal(valido, Validar(new VinculoAlta { NbEquipo = equipo, IdTrabajador = 1 }).Count == 0);
    }

    [Fact]
    public void Nombre_de_equipo_respeta_limite_de_MM_HORARIO()
    {
        Assert.NotEmpty(Validar(new VinculoAlta { NbEquipo = new string('A', 51), IdTrabajador = 1 }));
    }
}
