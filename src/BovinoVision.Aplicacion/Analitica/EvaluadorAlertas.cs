using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Pesajes;

namespace BovinoVision.Aplicacion.Analitica;

/// <summary>
/// Applies the GMD alert rule to a newly registered weighing event. The event is only
/// staged by the caller, so the previous weighing comes from persisted data.
/// </summary>
public sealed class EvaluadorAlertas(
    IRepositorioAlertas repositorio,
    CalculadoraGmd calculadora,
    ReglasAlerta reglas,
    TimeProvider reloj) : IEvaluadorAlertas
{
    public async Task<Alerta?> EvaluarAsync(EventoPesaje evento, CancellationToken cancelacion = default)
    {
        if (await repositorio.ExisteParaEventoAsync(evento.Id, cancelacion))
        {
            return null;
        }

        var anterior = await repositorio.ObtenerPesajeAnteriorAsync(evento.IdAnimal, evento.FechaPesaje, cancelacion);
        if (anterior is null)
        {
            return null;
        }

        var gmd = calculadora.Calcular(anterior.Value, new Medicion(evento.PesoKg, evento.FechaPesaje));
        if (gmd is null)
        {
            return null;
        }

        var gmdObjetivo = evento.IdHistorialLote is { } idHistorialLote
            ? await repositorio.ObtenerGmdObjetivoAsync(idHistorialLote, cancelacion)
            : null;

        var alerta = reglas.Evaluar(evento.Id, gmd.Value, gmdObjetivo, reloj.GetUtcNow());
        if (alerta is not null)
        {
            repositorio.Agregar(alerta);
        }

        return alerta;
    }
}
