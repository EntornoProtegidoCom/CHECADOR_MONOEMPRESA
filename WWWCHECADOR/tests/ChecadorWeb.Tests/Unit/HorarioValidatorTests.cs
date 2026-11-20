using ChecadorWeb.Services;

namespace ChecadorWeb.Tests.Unit;

public class HorarioValidatorTests
{
    private static readonly HorariosOptions SinCruce = new() { PermitirCruceMedianoche = false, MinutosMinimosEntreMarcajes = 5 };
    private static readonly HorariosOptions ConCruce = new() { PermitirCruceMedianoche = true, MinutosMinimosEntreMarcajes = 5 };

    private static HorarioCaptura H(string? e, string? sc, string? ec, string? s) =>
        new(T(e), T(sc), T(ec), T(s));

    private static TimeOnly? T(string? v) => v is null ? null : TimeOnly.Parse(v);

    [Fact]
    public void Horario_normal_es_valido() =>
        Assert.Empty(HorarioValidator.Validar(H("08:00", "13:00", "14:00", "17:00"), SinCruce));

    [Fact]
    public void Horario_sin_comida_es_valido() =>
        Assert.Empty(HorarioValidator.Validar(H("08:00", null, null, "17:00"), SinCruce));

    [Fact]
    public void Entrada_y_salida_son_obligatorias()
    {
        var errores = HorarioValidator.Validar(H(null, null, null, null), SinCruce);
        Assert.Contains(errores, e => e.Campo == "Entrada");
        Assert.Contains(errores, e => e.Campo == "Salida");
    }

    [Fact]
    public void Comida_requiere_ambas_horas()
    {
        var errores = HorarioValidator.Validar(H("08:00", "13:00", null, "17:00"), SinCruce);
        Assert.Equal("SalidaComida", Assert.Single(errores).Campo);
    }

    [Theory]
    [InlineData("08:00", "13:00", "12:00", "17:00", "EntradaComida")]
    [InlineData("08:00", "07:00", "14:00", "17:00", "SalidaComida")]
    [InlineData("08:00", "13:00", "14:00", "13:30", "Salida")]
    [InlineData("08:00", null, null, "08:00", "Salida")]
    public void Orden_incorrecto_es_invalido(string e, string? sc, string? ec, string s, string campo)
    {
        var errores = HorarioValidator.Validar(H(e, sc, ec, s), SinCruce);
        Assert.Equal(campo, Assert.Single(errores).Campo);
    }

    [Fact]
    public void Cruce_de_medianoche_se_rechaza_cuando_no_esta_permitido()
    {
        var errores = HorarioValidator.Validar(H("22:00", "02:00", "03:00", "06:00"), SinCruce);
        Assert.Contains("medianoche", Assert.Single(errores).Mensaje);
    }

    [Fact]
    public void Cruce_de_medianoche_se_acepta_cuando_esta_permitido()
    {
        var h = H("22:00", "02:00", "03:00", "06:00");
        Assert.Empty(HorarioValidator.Validar(h, ConCruce));
        Assert.True(HorarioValidator.CruzaMedianoche(h));
    }

    [Fact]
    public void Con_cruce_permitido_sigue_validando_el_orden()
    {
        var errores = HorarioValidator.Validar(H("22:00", "03:00", "02:00", "06:00"), ConCruce);
        Assert.Equal("EntradaComida", Assert.Single(errores).Campo);
    }

    [Fact]
    public void Con_cruce_permitido_no_admite_jornada_de_24_horas()
    {
        Assert.NotEmpty(HorarioValidator.Validar(H("08:00", null, null, "08:00"), ConCruce));
    }

    [Theory]
    [InlineData("08:00", "08:04", "13:00", "17:00")]
    [InlineData("12:51", "13:00", "13:03", "13:30")]
    public void Exige_minimo_de_minutos_entre_marcajes(string e, string sc, string ec, string s)
    {
        var errores = HorarioValidator.Validar(H(e, sc, ec, s), SinCruce);
        Assert.Contains("al menos 5 minutos", Assert.Single(errores).Mensaje);
    }

    [Fact]
    public void Horarios_existentes_en_la_base_son_validos()
    {
        // Muestras reales de MM_TRABAJADORES (ninguna cruza medianoche).
        Assert.Empty(HorarioValidator.Validar(H("13:15", "13:22", "13:29", "13:36"), SinCruce));
        Assert.Empty(HorarioValidator.Validar(H("12:51", "13:00", "13:10", "13:30"), SinCruce));
        Assert.Empty(HorarioValidator.Validar(H("07:30", "12:30", "13:30", "16:30"), SinCruce));
    }
}
