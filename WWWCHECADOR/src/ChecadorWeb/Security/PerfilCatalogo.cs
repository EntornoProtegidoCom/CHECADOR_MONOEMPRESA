using ChecadorWeb.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ChecadorWeb.Security;

public sealed record PerfilItem(int Id, string Nombre);

public interface IPerfilCatalogo
{
    Task<IReadOnlyList<PerfilItem>> ObtenerAsync(CancellationToken ct = default);
}

/// <summary>
/// Catálogo de perfiles. Usa dbo.SS_PERFIL cuando exista (estructura confirmada por el negocio, aún por crear);
/// mientras tanto usa los nombres configurados en Seguridad:NombresPerfil. No crea la tabla.
/// </summary>
public sealed class PerfilCatalogo(
    ChecadorDbContext db,
    IMemoryCache cache,
    IOptionsMonitor<SeguridadOptions> opciones,
    ILogger<PerfilCatalogo> logger) : IPerfilCatalogo
{
    private const string ClaveCache = "catalogo-perfiles";

    public async Task<IReadOnlyList<PerfilItem>> ObtenerAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(ClaveCache, out IReadOnlyList<PerfilItem>? enCache) && enCache is not null)
            return enCache;

        var perfiles = new Dictionary<int, string>();
        foreach (var (clave, nombre) in opciones.CurrentValue.NombresPerfil)
            if (int.TryParse(clave, out var id)) perfiles[id] = nombre;

        var existeTabla = await db.Database
            .SqlQueryRaw<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.SS_PERFIL', N'U') IS NULL THEN 0 ELSE 1 END AS [Value]")
            .SingleAsync(ct);

        if (existeTabla == 1)
        {
            try
            {
                var filas = await db.Database
                    .SqlQueryRaw<PerfilFila>("SELECT ID_PERFIL AS Id, CAST(NB_NOMBRE AS nvarchar(100)) AS Nombre FROM dbo.SS_PERFIL")
                    .ToListAsync(ct);
                foreach (var f in filas) perfiles[f.Id] = f.Nombre;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "No fue posible leer dbo.SS_PERFIL; se usan los nombres configurados.");
            }
        }

        IReadOnlyList<PerfilItem> resultado = perfiles.OrderBy(p => p.Key).Select(p => new PerfilItem(p.Key, p.Value)).ToList();
        cache.Set(ClaveCache, resultado, TimeSpan.FromMinutes(5));
        return resultado;
    }

    private sealed class PerfilFila
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }
}
