using System.Threading.RateLimiting;
using ChecadorWeb.Data;
using ChecadorWeb.Infrastructure;
using ChecadorWeb.Security;
using ChecadorWeb.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// La cadena de conexión se obtiene de user-secrets (desarrollo) o de la variable de entorno
// ConnectionStrings__ChecadorDb (servidores). Nunca se guarda en appsettings.
var cadena = builder.Configuration.GetConnectionString("ChecadorDb");
if (string.IsNullOrWhiteSpace(cadena) && !builder.Environment.IsEnvironment("Testing"))
    throw new InvalidOperationException(
        "Falta ConnectionStrings:ChecadorDb. Configúrela con 'dotnet user-secrets' o la variable de entorno ConnectionStrings__ChecadorDb.");

var nivelCompatibilidad = builder.Configuration.GetValue("BaseDatos:NivelCompatibilidad", 150);
builder.Services.AddDbContext<ChecadorDbContext>(o => o
    .UseSqlServer(cadena ?? "Server=(no-configurado)", sql => sql.UseCompatibilityLevel(nivelCompatibilidad).CommandTimeout(30))
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

builder.Services.Configure<SeguridadOptions>(builder.Configuration.GetSection(SeguridadOptions.Seccion));
builder.Services.Configure<HeartbeatOptions>(builder.Configuration.GetSection(HeartbeatOptions.Seccion));
builder.Services.Configure<HorariosOptions>(builder.Configuration.GetSection(HorariosOptions.Seccion));
builder.Services.Configure<AuditoriaOptions>(builder.Configuration.GetSection(AuditoriaOptions.Seccion));

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IPermisoResolver, PermisoResolver>();
builder.Services.AddSingleton<IAuthorizationHandler, PermisoAuthorizationHandler>();
builder.Services.AddScoped<ValidadorSesionCookie>();
builder.Services.AddScoped<IAutenticacionService, AutenticacionService>();
builder.Services.AddScoped<IPerfilCatalogo, PerfilCatalogo>();
builder.Services.AddScoped<IAuditoria, AuditoriaService>();
builder.Services.AddScoped<IEmpleadoService, EmpleadoService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IVinculoService, VinculoService>();
builder.Services.AddScoped<IEstacionService, EstacionService>();
builder.Services.AddScoped<IMarcajeService, MarcajeService>();
builder.Services.AddScoped<IResumenHorasService, ResumenHorasService>();
builder.Services.AddScoped<IPanelService, PanelService>();
builder.Services.AddScoped<IDiaFestivoService, DiaFestivoService>();
builder.Services.AddScoped<IVacacionService, VacacionService>();

var seguridad = builder.Configuration.GetSection(SeguridadOptions.Seccion).Get<SeguridadOptions>() ?? new SeguridadOptions();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "__Host-ChecadorWeb";
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.Path = "/";
        o.ExpireTimeSpan = TimeSpan.FromMinutes(seguridad.MinutosSesion);
        o.SlidingExpiration = true;
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/AccesoDenegado";
        o.EventsType = typeof(ValidadorSesionCookie);
    });

builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    foreach (var permiso in Permisos.Todos)
        o.AddPolicy(permiso, p => p.RequireAuthenticatedUser().AddRequirements(new PermisoRequirement(permiso)));
});

builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "__Host-ChecadorWeb-AF";
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Math.Max(1, seguridad.IntentosLoginPorMinuto), Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AllowAnonymousToPage("/Account/Login");
    o.Conventions.AllowAnonymousToPage("/Account/AccesoDenegado");
    o.Conventions.AllowAnonymousToPage("/Error");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseCabecerasSeguridad();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();

public partial class Program;
