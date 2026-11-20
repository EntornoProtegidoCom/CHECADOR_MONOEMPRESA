using Microsoft.OpenApi.Models;
using ChecadorAPI.Data;
using ChecadorAPI.Middleware;
using ChecadorAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Controladores con formateo JSON
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configuracion de Swagger UI con soporte para ApiKey
builder.Services.AddSwaggerGen(c =>
{
    var apiVersion = builder.Configuration.GetValue<string>("ApiSettings:ApiVersion") ?? "v1.0.0";
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Checador API REST",
        Version = apiVersion.ToString(),// "v1",
        Description = "API de Asistencia y Control Biometrico para ChecadorDesktop"
    });

    // Definir seguridad por encabezado X-Api-Key
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Encabezado de autorizacion API Key. Ejemplo: 'X-Api-Key: TU_CLAVE'",
        In = ParameterLocation.Header,
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKeyScheme"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                },
                Scheme = "ApiKeyScheme",
                Name = "X-Api-Key",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

// Registrar fabrica de conexiones
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBindingService, BindingService>();
builder.Services.AddScoped<IAsistenciaService, AsistenciaService>();
builder.Services.AddScoped<IWorkerScheduleService, WorkerScheduleService>();

var app = builder.Build();

var needPin = app.Configuration.GetValue<bool?>("ApiSettings:NeedPin") ?? true;
app.Logger.LogInformation("Configuracion ApiSettings:NeedPin = {NeedPin}", needPin);

// Habilitar Swagger UI siempre (incluso en produccion para pruebas locales)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Checador API v1");
    c.RoutePrefix = "swagger";
});

// Middleware de seguridad para ApiKey
app.UseMiddleware<ApiKeyMiddleware>();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
