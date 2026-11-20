using System.Security.Claims;
using System.Text.Encodings.Web;
using ChecadorWeb.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChecadorWeb.Tests.Web;

/// <summary>
/// Perfiles de prueba (solo existen en la configuración de pruebas):
/// 1 = todos, 2 = empleados.ver, 3 = estaciones.ver, 4 = marcajes.ver, 9 = sin permisos.
/// </summary>
public class ChecadorWebFactory : WebApplicationFactory<Program>
{
    public const string HeaderUsuario = "X-Test-Usuario";
    public const string HeaderPerfil = "X-Test-Perfil";

    public static string? CadenaConexion { get; } = ObtenerCadena();
    public static bool HayBaseDatos { get; } = ProbarConexion();

    public string CarpetaAuditoria { get; } = Path.Combine(Path.GetTempPath(), "checadorweb-tests", Guid.NewGuid().ToString("N"));

    protected virtual int IntentosLogin => 1000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ChecadorDb", CadenaConexion ?? "Server=(no-configurado);Connect Timeout=1");
        builder.UseSetting("Auditoria:CarpetaArchivo", CarpetaAuditoria);
        builder.UseSetting("Seguridad:IntentosLoginPorMinuto", IntentosLogin.ToString());
        builder.UseSetting("Seguridad:PermisosPorPerfil:2:0", Permisos.EmpleadosVer);
        builder.UseSetting("Seguridad:PermisosPorPerfil:3:0", Permisos.EstacionesVer);
        builder.UseSetting("Seguridad:PermisosPorPerfil:4:0", Permisos.MarcajesVer);
        builder.ConfigureLogging(l => l.SetMinimumLevel(LogLevel.Warning));

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, PruebaAuthHandler>(PruebaAuthHandler.Esquema, _ => { })
                .AddPolicyScheme("Selector", "Selector", o => o.ForwardDefaultSelector = ctx =>
                    ctx.Request.Headers.ContainsKey(HeaderPerfil) ? PruebaAuthHandler.Esquema : CookieAuthenticationDefaults.AuthenticationScheme);
            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = "Selector";
                o.DefaultAuthenticateScheme = "Selector";
                o.DefaultChallengeScheme = "Selector";
                o.DefaultForbidScheme = "Selector";
            });
        });
    }

    public HttpClient Cliente(int? perfil = null, string usuario = "prueba.web")
    {
        var cliente = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });
        if (perfil is { } p)
        {
            cliente.DefaultRequestHeaders.Add(HeaderPerfil, p.ToString());
            cliente.DefaultRequestHeaders.Add(HeaderUsuario, usuario);
        }
        return cliente;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { if (Directory.Exists(CarpetaAuditoria)) Directory.Delete(CarpetaAuditoria, true); } catch (IOException) { }
    }

    private static string? ObtenerCadena()
    {
        var env = Environment.GetEnvironmentVariable("CHECADOR_TEST_DB");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        var cfg = new ConfigurationBuilder().AddUserSecrets(typeof(Program).Assembly, optional: true).Build();
        return cfg.GetConnectionString("ChecadorDb");
    }

    private static bool ProbarConexion()
    {
        if (string.IsNullOrWhiteSpace(CadenaConexion)) return false;
        try
        {
            using var cn = new SqlConnection(new SqlConnectionStringBuilder(CadenaConexion) { ConnectTimeout = 5 }.ConnectionString);
            cn.Open();
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
    }
}

public sealed class PruebaAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Esquema = "Prueba";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ChecadorWebFactory.HeaderPerfil, out var perfil))
            return Task.FromResult(AuthenticateResult.NoResult());

        var usuario = Request.Headers[ChecadorWebFactory.HeaderUsuario].ToString();
        var identidad = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, usuario),
            new Claim(ClaimTypes.Name, usuario),
            new Claim(ClaimTypesChecador.Perfil, perfil.ToString()),
        ], Esquema);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema)));
    }
}

public sealed class FactoryLimiteLogin : ChecadorWebFactory
{
    protected override int IntentosLogin => 3;
}
