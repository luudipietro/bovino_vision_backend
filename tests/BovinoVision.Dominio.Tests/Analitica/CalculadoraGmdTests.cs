using BovinoVision.Dominio.Analitica;

namespace BovinoVision.Dominio.Tests.Analitica;

public class CalculadoraGmdTests
{
    // Zona UTC-3 fija para que los tests no dependan de la base de zonas horarias de la máquina.
    private static readonly TimeZoneInfo UtcMenos3 =
        TimeZoneInfo.CreateCustomTimeZone("UTC-3", TimeSpan.FromHours(-3), "UTC-3", "UTC-3");

    private static readonly DateTimeOffset Inicio = new(2026, 1, 1, 13, 0, 0, TimeSpan.Zero);

    private readonly CalculadoraGmd _calculadora = new(UtcMenos3);

    [Fact]
    public void Calcula_la_ganancia_diaria_entre_dos_pesajes()
    {
        var gmd = _calculadora.Calcular(new Medicion(300m, Inicio), new Medicion(320m, Inicio.AddDays(10)));

        Assert.Equal(2.00m, gmd);
    }

    [Fact]
    public void Una_perdida_de_peso_da_ganancia_negativa()
    {
        var gmd = _calculadora.Calcular(new Medicion(320m, Inicio), new Medicion(300m, Inicio.AddDays(4)));

        Assert.Equal(-5.00m, gmd);
    }

    [Fact]
    public void Dos_pesajes_del_mismo_dia_no_tienen_gmd()
    {
        var gmd = _calculadora.Calcular(new Medicion(300m, Inicio), new Medicion(320m, Inicio.AddHours(5)));

        Assert.Null(gmd);
    }

    [Fact]
    public void Un_pesaje_posterior_en_fecha_anterior_no_tiene_gmd()
    {
        var gmd = _calculadora.Calcular(new Medicion(300m, Inicio.AddDays(3)), new Medicion(320m, Inicio));

        Assert.Null(gmd);
    }

    [Fact]
    public void Los_dias_se_cuentan_por_fecha_calendario_de_la_zona_configurada()
    {
        // Las 02:00 UTC del 2 de enero todavía son las 23:00 del 1 de enero en UTC-3, así que ambos pesajes comparten día.
        var mismaFechaLocal = _calculadora.Calcular(
            new Medicion(300m, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)),
            new Medicion(310m, new DateTimeOffset(2026, 1, 2, 2, 0, 0, TimeSpan.Zero)));

        // Las 23:00 de la noche y las 09:00 de la mañana siguiente (hora local) están a un día calendario de distancia.
        var diaSiguienteLocal = _calculadora.Calcular(
            new Medicion(300m, new DateTimeOffset(2026, 1, 1, 23, 0, 0, TimeSpan.FromHours(-3))),
            new Medicion(301.5m, new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.FromHours(-3))));

        Assert.Null(mismaFechaLocal);
        Assert.Equal(1.50m, diaSiguienteLocal);
    }

    [Fact]
    public void Fecha_local_convierte_el_instante_a_la_zona_configurada()
    {
        var fecha = _calculadora.FechaLocal(new DateTimeOffset(2026, 1, 2, 2, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 1, 1), fecha);
    }

    [Theory]
    [InlineData(300, 320, 3, 6.67)]
    [InlineData(300, 300.25, 10, 0.02)] // 0.025 rounds half to even
    [InlineData(300, 300.35, 10, 0.04)] // 0.035 rounds half to even
    public void Redondea_la_gmd_a_dos_decimales_al_par_mas_cercano(double anterior, double siguiente, int dias, double esperada)
    {
        var gmd = _calculadora.Calcular(
            new Medicion((decimal)anterior, Inicio),
            new Medicion((decimal)siguiente, Inicio.AddDays(dias)));

        Assert.Equal((decimal)esperada, gmd);
    }

    [Fact]
    public void Promedia_con_un_decimal_al_par_mas_cercano()
    {
        Assert.Equal(360.0m, CalculadoraGmd.Promediar([320m, 400m]));
        Assert.Equal(2.2m, CalculadoraGmd.Promediar([2.2m, 2.3m])); // 2.25 rounds half to even
        Assert.Null(CalculadoraGmd.Promediar([]));
    }

    [Fact]
    public void Gmd_entre_consecutivos_ordena_por_fecha_y_omite_los_del_mismo_dia()
    {
        Medicion[] mediciones =
        [
            new(320m, Inicio.AddDays(10)),
            new(300m, Inicio),
            new(330m, Inicio.AddDays(10).AddHours(2)),
            new(340m, Inicio.AddDays(15)),
        ];

        var gmds = _calculadora.GmdEntreConsecutivos(mediciones);

        Assert.Equal([2.00m, 2.00m], gmds);
    }
}
