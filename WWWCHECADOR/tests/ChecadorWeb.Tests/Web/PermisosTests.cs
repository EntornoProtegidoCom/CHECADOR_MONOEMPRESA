using System.Net;
using System.Text.RegularExpressions;

namespace ChecadorWeb.Tests.Web;

public partial class PermisosTests(ChecadorWebFactory factory) : IClassFixture<ChecadorWebFactory>
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Empleados")]
    [InlineData("/Usuarios")]
    [InlineData("/Estaciones?handler=Datos")]
    [InlineData("/Marcajes?handler=Foto&id=1")]
    [InlineData("/Marcajes?handler=Excel")]
    public async Task Anonimo_es_redirigido_al_login(string url)
    {
        var r = await factory.Cliente().GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Account/Login", r.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Login_es_publico_y_envia_cabeceras_de_seguridad()
    {
        var r = await factory.Cliente().GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("frame-ancestors 'none'", r.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());

        var cookieAf = r.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-ChecadorWeb-AF"));
        Assert.Contains("secure", cookieAf, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookieAf, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookieAf, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/Usuarios")]
    [InlineData("/Usuarios/Crear")]
    [InlineData("/Usuarios/Editar/alguien")]
    [InlineData("/Usuarios/Password/alguien")]
    [InlineData("/Empleados/Crear")]
    [InlineData("/Empleados/Editar/1")]
    [InlineData("/Horarios")]
    [InlineData("/Horarios/Editar/1")]
    [InlineData("/Equipos")]
    [InlineData("/Equipos/Vincular")]
    [InlineData("/Estaciones")]
    [InlineData("/Estaciones?handler=Datos")]
    [InlineData("/Marcajes")]
    [InlineData("/Marcajes?handler=Excel")]
    public async Task Perfil_de_consulta_de_empleados_no_accede_a_otros_modulos(string url)
    {
        var r = await factory.Cliente(perfil: 2).GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Theory]
    [InlineData("/Empleados")]
    [InlineData("/Estaciones")]
    [InlineData("/Marcajes")]
    [InlineData("/Usuarios")]
    public async Task Perfil_sin_permisos_configurados_no_accede(string url)
    {
        var r = await factory.Cliente(perfil: 9).GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Foto_de_marcaje_requiere_permiso_especifico()
    {
        var r = await factory.Cliente(perfil: 4).GetAsync("/Marcajes?handler=Foto&id=1");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Theory]
    [InlineData("/Empleados?handler=Estado&id=1&activar=false")]
    [InlineData("/Usuarios?handler=Estado&id=x&activar=false")]
    [InlineData("/Equipos?handler=Desvincular&id=1")]
    [InlineData("/Empleados/Crear")]
    [InlineData("/Account/Logout")]
    public async Task Post_sin_token_antiforgery_es_rechazado(string url)
    {
        var r = await factory.Cliente(perfil: 1).PostAsync(url, new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [SkippableFact]
    public async Task Handler_de_escritura_valida_permiso_aunque_la_pagina_sea_visible()
    {
        Skip.IfNot(ChecadorWebFactory.HayBaseDatos, "Sin acceso a la base de datos de pruebas.");
        var cliente = factory.Cliente(perfil: 2);

        var pagina = await cliente.GetAsync("/Empleados");
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        var html = await pagina.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Nuevo empleado", html);
        Assert.DoesNotContain("asp-page-handler", html);
        Assert.DoesNotContain("handler=Estado", html);

        var token = Token(html);
        var r = await cliente.PostAsync("/Empleados?handler=Estado&id=1&activar=false",
            new FormUrlEncodedContent([new("__RequestVerificationToken", token)]));
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [SkippableTheory]
    [InlineData("/")]
    [InlineData("/Empleados")]
    [InlineData("/Empleados?Estado=todos&Texto=a&Pagina=1&Tamano=10")]
    [InlineData("/Empleados/Crear")]
    [InlineData("/Empleados/Editar/2")]
    [InlineData("/Horarios")]
    [InlineData("/Horarios?SinHorario=true")]
    [InlineData("/Horarios/Editar/2")]
    [InlineData("/Equipos")]
    [InlineData("/Equipos?Estado=todos")]
    [InlineData("/Equipos/Vincular")]
    [InlineData("/Estaciones")]
    [InlineData("/Estaciones?aviso=5&desconexion=30")]
    [InlineData("/Marcajes")]
    [InlineData("/Marcajes?Desde=2020-01-01&Hasta=2026-12-31&Tipo=1")]
    [InlineData("/Usuarios")]
    [InlineData("/Usuarios/Crear")]
    [InlineData("/Account/MiCuenta")]
    public async Task Administrador_accede_a_todas_las_paginas_con_datos_reales(string url)
    {
        Skip.IfNot(ChecadorWebFactory.HayBaseDatos, "Sin acceso a la base de datos de pruebas.");
        var r = await factory.Cliente(perfil: 1).GetAsync(url);
        var html = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.OK, $"{url} devolvió {(int)r.StatusCode}");
        Assert.DoesNotContain("No fue posible consultar", html);
    }

    [SkippableFact]
    public async Task Datos_de_estaciones_devuelven_json_con_hora_del_servidor()
    {
        Skip.IfNot(ChecadorWebFactory.HayBaseDatos, "Sin acceso a la base de datos de pruebas.");
        var r = await factory.Cliente(perfil: 3).GetAsync("/Estaciones?handler=Datos&aviso=3&desconexion=15");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var json = await r.Content.ReadAsStringAsync();
        Assert.Contains("\"ahora\"", json);
        Assert.Contains("\"filas\"", json);
    }

    [SkippableFact]
    public async Task Paginas_no_exponen_pin_ni_contrasenas()
    {
        Skip.IfNot(ChecadorWebFactory.HayBaseDatos, "Sin acceso a la base de datos de pruebas.");
        var cliente = factory.Cliente(perfil: 1);
        foreach (var url in new[] { "/Empleados?Estado=todos&Tamano=100", "/Empleados/Editar/2", "/Usuarios?Estado=todos" })
        {
            var html = await cliente.GetStringAsync(url);
            Assert.DoesNotContain("URGL", html);
            Assert.DoesNotMatch(HashHex(), html);
        }
    }

    private static string Token(string html) =>
        TokenRegex().Match(html) is { Success: true } m ? m.Groups[1].Value : throw new InvalidOperationException("No se encontró token antiforgery.");

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();

    [GeneratedRegex("[0-9A-Fa-f]{64}")]
    private static partial Regex HashHex();
}

public class LimiteLoginTests(FactoryLimiteLogin factory) : IClassFixture<FactoryLimiteLogin>
{
    [Fact]
    public async Task Login_limita_intentos_por_minuto()
    {
        var cliente = factory.Cliente();
        var codigos = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++) codigos.Add((await cliente.GetAsync("/Account/Login")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, codigos[0]);
        Assert.Contains(HttpStatusCode.TooManyRequests, codigos);
    }
}
