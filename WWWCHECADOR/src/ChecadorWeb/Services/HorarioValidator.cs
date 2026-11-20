namespace ChecadorWeb.Services;

public sealed class HorariosOptions
{
    public const string Seccion = "Horarios";

    /// <summary>
    /// Permite jornadas que cruzan medianoche. Desactivado por decisión del negocio:
    /// sp_registro_checador agrupa los marcajes por día calendario.
    /// </summary>
    public bool PermitirCruceMedianoche { get; set; }

    /// <summary>sp_registro_checador exige al menos 5 minutos entre dos marcajes del mismo día.</summary>
    public int MinutosMinimosEntreMarcajes { get; set; } = 5;
}

public sealed record HorarioCaptura(TimeOnly? Entrada, TimeOnly? SalidaComida, TimeOnly? EntradaComida, TimeOnly? Salida);

public sealed record ErrorHorario(string Campo, string Mensaje);

public static class HorarioValidator
{
    public const int MinutosDia = 24 * 60;

    public static IReadOnlyList<ErrorHorario> Validar(HorarioCaptura h, HorariosOptions opciones)
    {
        var errores = new List<ErrorHorario>();

        if (h.Entrada is null) errores.Add(new(nameof(h.Entrada), "La hora de entrada es obligatoria."));
        if (h.Salida is null) errores.Add(new(nameof(h.Salida), "La hora de salida es obligatoria."));
        if ((h.SalidaComida is null) != (h.EntradaComida is null))
            errores.Add(new(nameof(h.SalidaComida), "Capture ambas horas de comida o deje ambas vacías."));
        if (errores.Count > 0) return errores;

        var puntos = new List<(string Campo, string Etiqueta, TimeOnly Hora)> { (nameof(h.Entrada), "entrada", h.Entrada!.Value) };
        if (h.SalidaComida is not null)
        {
            puntos.Add((nameof(h.SalidaComida), "salida a comer", h.SalidaComida.Value));
            puntos.Add((nameof(h.EntradaComida), "entrada de comer", h.EntradaComida!.Value));
        }
        puntos.Add((nameof(h.Salida), "salida", h.Salida!.Value));

        var inicio = Minutos(puntos[0].Hora);
        var anterior = 0;
        for (var i = 1; i < puntos.Count; i++)
        {
            var actual = Minutos(puntos[i].Hora);
            int desplazamiento;
            if (opciones.PermitirCruceMedianoche)
            {
                desplazamiento = (actual - inicio + MinutosDia) % MinutosDia;
                if (desplazamiento == 0) desplazamiento = MinutosDia;
            }
            else
            {
                desplazamiento = actual - inicio;
            }

            if (desplazamiento <= anterior)
            {
                var mensaje = opciones.PermitirCruceMedianoche
                    ? $"La hora de {puntos[i].Etiqueta} debe ser posterior a la de {puntos[i - 1].Etiqueta} dentro de la misma jornada."
                    : $"La hora de {puntos[i].Etiqueta} debe ser posterior a la de {puntos[i - 1].Etiqueta}. No se permiten jornadas que crucen medianoche.";
                errores.Add(new(puntos[i].Campo, mensaje));
                return errores;
            }

            if (desplazamiento - anterior < opciones.MinutosMinimosEntreMarcajes)
            {
                errores.Add(new(puntos[i].Campo,
                    $"Entre {puntos[i - 1].Etiqueta} y {puntos[i].Etiqueta} debe haber al menos {opciones.MinutosMinimosEntreMarcajes} minutos (regla del checador)."));
                return errores;
            }

            anterior = desplazamiento;
        }

        if (anterior >= MinutosDia)
            errores.Add(new(nameof(h.Salida), "La jornada no puede durar 24 horas o más."));

        return errores;
    }

    public static bool CruzaMedianoche(HorarioCaptura h) =>
        h.Entrada is { } e && h.Salida is { } s && s <= e;

    private static int Minutos(TimeOnly t) => t.Hour * 60 + t.Minute;
}
