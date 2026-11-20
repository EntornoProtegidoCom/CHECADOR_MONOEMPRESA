using ChecadorWeb.Services;

namespace ChecadorWeb.Tests.Unit;

public class HeartbeatClasificadorTests
{
    private static readonly DateTime Ahora = new(2026, 11, 2, 12, 0, 0);

    [Theory]
    [InlineData(0, EstadoComunicacion.EnLinea)]
    [InlineData(2.9, EstadoComunicacion.EnLinea)]
    [InlineData(3, EstadoComunicacion.Aviso)]
    [InlineData(14.9, EstadoComunicacion.Aviso)]
    [InlineData(15, EstadoComunicacion.SinComunicacion)]
    [InlineData(600, EstadoComunicacion.SinComunicacion)]
    [InlineData(-1, EstadoComunicacion.EnLinea)]
    [InlineData(-10, EstadoComunicacion.HoraInconsistente)]
    public void Clasifica_por_antiguedad_del_heartbeat(double minutosAtras, EstadoComunicacion esperado)
    {
        var ultimo = Ahora.AddMinutes(-minutosAtras);
        Assert.Equal(esperado, HeartbeatClasificador.Clasificar(ultimo, Ahora, 3, 15));
    }

    [Fact]
    public void Sin_heartbeat_es_nunca_reporto_y_no_sin_comunicacion()
    {
        Assert.Equal(EstadoComunicacion.NuncaReporto, HeartbeatClasificador.Clasificar(null, Ahora, 3, 15));
    }

    [Theory]
    [InlineData(null, null, 3, 15)]
    [InlineData(5, 30, 5, 30)]
    [InlineData(10, 10, 10, 11)]
    [InlineData(0, 1, 1, 2)]
    [InlineData(-5, 99999, 1, 10080)]
    public void Normaliza_umbrales(int? aviso, int? desconexion, int esperadoAviso, int esperadoDesconexion)
    {
        var (a, d) = HeartbeatClasificador.NormalizarUmbrales(aviso, desconexion, new HeartbeatOptions());
        Assert.Equal(esperadoAviso, a);
        Assert.Equal(esperadoDesconexion, d);
    }
}
