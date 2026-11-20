namespace ChecadorWeb.Infrastructure;

public static class FormatoDuracion
{
    public const string Excel = "[hh]:mm:ss";

    public static double DiasParaExcel(double horas) => horas / 24;

    public static string Texto(double horas)
    {
        var segundos = checked((long)Math.Round(horas * 3600, MidpointRounding.AwayFromZero));
        return $"{segundos / 3600:00}:{segundos / 60 % 60:00}:{segundos % 60:00}";
    }
}
