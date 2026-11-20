namespace ChecadorWeb.Models;

public sealed class PaginaResultado<T>
{
    public required IReadOnlyList<T> Elementos { get; init; }
    public required int Pagina { get; init; }
    public required int TamanoPagina { get; init; }
    public required int Total { get; init; }

    public int TotalPaginas => Total == 0 ? 1 : (int)Math.Ceiling(Total / (double)TamanoPagina);
    public bool TieneAnterior => Pagina > 1;
    public bool TieneSiguiente => Pagina < TotalPaginas;
    public int Desde => Total == 0 ? 0 : (Pagina - 1) * TamanoPagina + 1;
    public int Hasta => Math.Min(Pagina * TamanoPagina, Total);
}

public static class Paginacion
{
    public const int TamanoPorDefecto = 20;
    public static readonly int[] TamanosPermitidos = [10, 20, 50, 100];

    public static int NormalizarTamano(int? tamano) =>
        tamano is { } t && TamanosPermitidos.Contains(t) ? t : TamanoPorDefecto;

    public static int NormalizarPagina(int? pagina) => pagina is > 0 ? pagina.Value : 1;
}

public enum TipoResultado { Exito, Advertencia, Error }

public sealed record ResultadoOperacion(TipoResultado Tipo, string Mensaje, string? Codigo = null)
{
    public bool Exitoso => Tipo == TipoResultado.Exito;
    public static ResultadoOperacion Ok(string mensaje) => new(TipoResultado.Exito, mensaje);
    public static ResultadoOperacion Advertencia(string mensaje) => new(TipoResultado.Advertencia, mensaje);
    public static ResultadoOperacion Fallo(string mensaje) => new(TipoResultado.Error, mensaje);
}

public static class TiposRegistro
{
    /// <summary>Catálogo usado por sp_registro_checador.</summary>
    public static readonly IReadOnlyDictionary<short, string> Nombres = new Dictionary<short, string>
    {
        [1] = "Entrada",
        [2] = "Salida a comer",
        [3] = "Entrada de comer",
        [4] = "Salida",
    };

    public static string Nombre(short tipo) => Nombres.TryGetValue(tipo, out var n) ? n : $"Desconocido ({tipo})";
}
