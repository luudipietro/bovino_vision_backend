using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Pesajes;

namespace BovinoVision.Aplicacion.Analitica;

/// <summary>
/// Evaluates a newly registered weighing event and stages an alert when the weight
/// trend is anomalous. It runs inside the caller's unit of work, so the event and its
/// alert are committed together; implementations must not save changes themselves.
/// </summary>
public interface IEvaluadorAlertas
{
    Task<Alerta?> EvaluarAsync(EventoPesaje evento, CancellationToken cancelacion = default);
}

/// <summary>Placeholder used until the Analítica module registers the real evaluator.</summary>
public sealed class EvaluadorAlertasNulo : IEvaluadorAlertas
{
    public Task<Alerta?> EvaluarAsync(EventoPesaje evento, CancellationToken cancelacion = default) =>
        Task.FromResult<Alerta?>(null);
}
