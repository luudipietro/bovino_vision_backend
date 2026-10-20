using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Aplicacion.Analitica;
using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Api.Tests.Analitica;

public class EvaluadorAlertasIntegracionTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task El_modulo_registra_el_evaluador_real()
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();

        Assert.IsType<EvaluadorAlertas>(alcance.ServiceProvider.GetRequiredService<IEvaluadorAlertas>());
    }

    [Fact]
    public async Task La_alerta_se_guarda_con_el_evento_en_la_misma_unidad_de_trabajo_y_no_se_duplica()
    {
        Guid idEvento;
        await using (var alcance = Fabrica.Services.CreateAsyncScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
            var datos = new DatosAnalitica(contexto);
            var lote = datos.CrearLote("L-EVAL", gmdObjetivo: 1.0m);
            var animal = datos.CrearAnimal("AR-EVAL-01");
            DatosAnalitica.AsignarLote(animal, lote, DatosAnalitica.Inicio.AddDays(-1));
            datos.Pesar(animal, 300m, DatosAnalitica.Inicio);
            await datos.GuardarAsync();

            // The new event is only staged when the evaluator runs, as in the weighing use case.
            var evento = datos.Pesar(animal, 303m, DatosAnalitica.Inicio.AddDays(10));
            idEvento = evento.Id;
            var evaluador = alcance.ServiceProvider.GetRequiredService<IEvaluadorAlertas>();

            var alerta = await evaluador.EvaluarAsync(evento, Cancelacion);
            var repetida = await evaluador.EvaluarAsync(evento, Cancelacion);

            Assert.NotNull(alerta);
            Assert.Null(repetida);
            Assert.Equal(0, await contexto.Alertas.CountAsync(Cancelacion));
            await alcance.ServiceProvider.GetRequiredService<IUnidadDeTrabajo>().GuardarCambiosAsync(Cancelacion);
        }

        await using (var alcance = Fabrica.Services.CreateAsyncScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
            var guardada = await contexto.Alertas.SingleAsync(Cancelacion);
            Assert.Equal(idEvento, guardada.IdEvento);
            Assert.Equal(TipoAlerta.Estancamiento, guardada.Tipo);
            Assert.Equal(2, guardada.Prioridad);
            Assert.Equal(0.3m, guardada.Variacion);
            Assert.Equal(1.0m, guardada.GmdEsperada);
            Assert.Equal("gmd-v1", guardada.VersionRegla);
            Assert.Equal(EstadoAlerta.Activa, guardada.Estado);

            var evento = await contexto.EventosPesaje.SingleAsync(e => e.Id == idEvento, Cancelacion);
            var evaluador = alcance.ServiceProvider.GetRequiredService<IEvaluadorAlertas>();
            Assert.Null(await evaluador.EvaluarAsync(evento, Cancelacion));
            await alcance.ServiceProvider.GetRequiredService<IUnidadDeTrabajo>().GuardarCambiosAsync(Cancelacion);
            Assert.Equal(1, await contexto.Alertas.CountAsync(Cancelacion));
        }
    }

    [Fact]
    public async Task Una_perdida_de_peso_genera_alerta_aunque_el_animal_no_tenga_lote()
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
        var datos = new DatosAnalitica(contexto);
        var animal = datos.CrearAnimal("AR-EVAL-02");
        datos.Pesar(animal, 320m, DatosAnalitica.Inicio);
        await datos.GuardarAsync();

        var evento = datos.Pesar(animal, 300m, DatosAnalitica.Inicio.AddDays(10));
        var alerta = await alcance.ServiceProvider.GetRequiredService<IEvaluadorAlertas>().EvaluarAsync(evento, Cancelacion);

        Assert.Equal(TipoAlerta.PerdidaPeso, alerta?.Tipo);
        Assert.Equal(1, alerta?.Prioridad);
        Assert.Equal(-2m, alerta?.Variacion);
    }
}
