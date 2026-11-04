using BovinoVision.Api.Modulos.Analitica;
using BovinoVision.Dominio.Analitica;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Api.Tests.Analitica;

public class ModuloAnaliticaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static ServiceProvider Construir(Dictionary<string, string?> valores)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var servicios = new ServiceCollection();
        new ModuloAnalitica().AgregarServicios(servicios, configuracion);
        return servicios.BuildServiceProvider();
    }

    [Fact]
    public void Sin_configuracion_usa_la_zona_de_buenos_aires_y_los_parametros_por_defecto()
    {
        using var proveedor = Construir([]);

        var calculadora = proveedor.GetRequiredService<CalculadoraGmd>();
        var reglas = proveedor.GetRequiredService<ReglasAlerta>();

        Assert.Equal(TimeSpan.FromHours(-3), calculadora.ZonaHoraria.GetUtcOffset(Ahora));
        Assert.Equal(1, reglas.Evaluar(Guid.CreateVersion7(), -1m, null, Ahora)?.Prioridad);
        Assert.Equal(2, reglas.Evaluar(Guid.CreateVersion7(), 0.4m, 1m, Ahora)?.Prioridad);
        Assert.Null(reglas.Evaluar(Guid.CreateVersion7(), 0.6m, 1m, Ahora));
    }

    [Fact]
    public void Lee_la_zona_horaria_y_los_parametros_de_alertas_de_la_configuracion()
    {
        using var proveedor = Construir(new()
        {
            ["Analitica:ZonaHoraria"] = "UTC",
            ["Alertas:FraccionEstancamiento"] = "0.8",
            ["Alertas:PrioridadPerdida"] = "3",
            ["Alertas:PrioridadEstancamiento"] = "4",
        });

        var calculadora = proveedor.GetRequiredService<CalculadoraGmd>();
        var reglas = proveedor.GetRequiredService<ReglasAlerta>();

        Assert.Equal(TimeSpan.Zero, calculadora.ZonaHoraria.GetUtcOffset(Ahora));
        Assert.Equal(3, reglas.Evaluar(Guid.CreateVersion7(), -1m, null, Ahora)?.Prioridad);
        Assert.Equal(4, reglas.Evaluar(Guid.CreateVersion7(), 0.7m, 1m, Ahora)?.Prioridad);
    }
}
