namespace ChecadorAPI.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string ApiKeyHeaderName = "X-Api-Key";

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        // Excluir endpoints publicos como Swagger y HealthCheck
        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;
        if (path.StartsWith("/swagger") || path.Contains("/api/health"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            if (context.Request.Path.StartsWithSegments("/api/workers", StringComparison.OrdinalIgnoreCase))
            {
                await context.Response.WriteAsJsonAsync(ChecadorAPI.Models.DTOs.ApiResponseDto<object>.Error("Encabezado X-Api-Key no proporcionado."));
                return;
            }
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Encabezado X-Api-Key no proporcionado." });
            return;
        }

        var configuredApiKey = configuration.GetValue<string>("ApiSettings:ApiKey");

        if (!string.Equals(configuredApiKey, extractedApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            if (context.Request.Path.StartsWithSegments("/api/workers", StringComparison.OrdinalIgnoreCase))
            {
                await context.Response.WriteAsJsonAsync(ChecadorAPI.Models.DTOs.ApiResponseDto<object>.Error("X-Api-Key invalida."));
                return;
            }
            await context.Response.WriteAsJsonAsync(new { success = false, message = "X-Api-Key invalida." });
            return;
        }

        await _next(context);
    }
}
