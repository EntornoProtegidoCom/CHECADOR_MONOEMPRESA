namespace ChecadorWeb.Models;

public sealed record PaginadorVista(
    int Pagina, int TotalPaginas, int Total, int Desde, int Hasta, int Tamano,
    IReadOnlyDictionary<string, string?> Filtros)
{
    public static PaginadorVista De<T>(PaginaResultado<T> r, IDictionary<string, string?> filtros) =>
        new(r.Pagina, r.TotalPaginas, r.Total, r.Desde, r.Hasta, r.TamanoPagina,
            filtros.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToDictionary(f => f.Key, f => f.Value));

    public Dictionary<string, string?> Ruta(int pagina, int? tamano = null)
    {
        var ruta = new Dictionary<string, string?>(Filtros)
        {
            ["Pagina"] = pagina.ToString(),
            ["Tamano"] = (tamano ?? Tamano).ToString(),
        };
        return ruta;
    }
}
