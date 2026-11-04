using BovinoVision.Dominio.Analitica;

namespace BovinoVision.Dominio.Tests.Analitica;

public class ReglasAlertaTests
{
    private static readonly Guid IdEvento = Guid.CreateVersion7();
    private static readonly DateTimeOffset Ahora = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ReglasAlerta _reglas = new(new ParametrosReglasAlerta());

    [Fact]
    public void Una_gmd_negativa_genera_alerta_de_perdida_de_peso_con_prioridad_1()
    {
        var alerta = _reglas.Evaluar(IdEvento, -0.5m, gmdObjetivo: 1.2m, Ahora);

        Assert.NotNull(alerta);
        Assert.Equal(TipoAlerta.PerdidaPeso, alerta.Tipo);
        Assert.Equal(1, alerta.Prioridad);
        Assert.Equal(-0.5m, alerta.Variacion);
        Assert.Equal(IdEvento, alerta.IdEvento);
        Assert.Equal(EstadoAlerta.Activa, alerta.Estado);
        Assert.Equal(ReglasAlerta.Version, alerta.VersionRegla);
        Assert.Equal("gmd-v1", alerta.VersionRegla);
        Assert.Equal(Ahora, alerta.FechaGeneracion);
    }

    [Fact]
    public void Una_perdida_de_peso_sin_gmd_objetivo_tambien_genera_alerta()
    {
        var alerta = _reglas.Evaluar(IdEvento, -0.1m, gmdObjetivo: null, Ahora);

        Assert.Equal(TipoAlerta.PerdidaPeso, alerta?.Tipo);
        Assert.Null(alerta?.GmdEsperada);
    }

    [Fact]
    public void Una_gmd_por_debajo_de_la_fraccion_del_objetivo_genera_estancamiento_con_prioridad_2()
    {
        var alerta = _reglas.Evaluar(IdEvento, 0.4m, gmdObjetivo: 1.0m, Ahora);

        Assert.NotNull(alerta);
        Assert.Equal(TipoAlerta.Estancamiento, alerta.Tipo);
        Assert.Equal(2, alerta.Prioridad);
        Assert.Equal(0.4m, alerta.Variacion);
        Assert.Equal(1.0m, alerta.GmdEsperada);
    }

    [Fact]
    public void Una_gmd_igual_a_la_fraccion_del_objetivo_no_genera_alerta()
    {
        Assert.Null(_reglas.Evaluar(IdEvento, 0.5m, gmdObjetivo: 1.0m, Ahora));
    }

    [Fact]
    public void Sin_gmd_objetivo_una_gmd_baja_no_genera_estancamiento()
    {
        Assert.Null(_reglas.Evaluar(IdEvento, 0.0m, gmdObjetivo: null, Ahora));
    }

    [Fact]
    public void Una_gmd_normal_no_genera_alerta()
    {
        Assert.Null(_reglas.Evaluar(IdEvento, 1.1m, gmdObjetivo: 1.0m, Ahora));
    }

    [Fact]
    public void Usa_los_parametros_configurados()
    {
        var reglas = new ReglasAlerta(new ParametrosReglasAlerta
        {
            FraccionEstancamiento = 0.8m,
            PrioridadPerdida = 3,
            PrioridadEstancamiento = 4,
        });

        var estancamiento = reglas.Evaluar(IdEvento, 0.7m, gmdObjetivo: 1.0m, Ahora);
        var perdida = reglas.Evaluar(IdEvento, -1m, gmdObjetivo: 1.0m, Ahora);

        Assert.Equal(TipoAlerta.Estancamiento, estancamiento?.Tipo);
        Assert.Equal(4, estancamiento?.Prioridad);
        Assert.Equal(3, perdida?.Prioridad);
    }

    [Fact]
    public void Rechaza_una_fraccion_de_estancamiento_no_positiva()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReglasAlerta(new ParametrosReglasAlerta { FraccionEstancamiento = 0m }));
    }
}
