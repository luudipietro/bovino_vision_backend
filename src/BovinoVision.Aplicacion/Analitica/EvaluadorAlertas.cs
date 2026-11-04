using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Pesajes;

namespace BovinoVision.Aplicacion.Analitica;

/// <summary>
/// Aplica la regla de alerta por GMD a un evento de pesaje recién registrado. El llamador solo
/// prepara el evento, así que el pesaje anterior sale de los datos persistidos.
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
