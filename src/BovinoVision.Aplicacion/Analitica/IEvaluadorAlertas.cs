using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Pesajes;

namespace BovinoVision.Aplicacion.Analitica;

/// <summary>
/// Evalúa un evento de pesaje recién registrado y prepara una alerta cuando la tendencia
/// del peso es anómala. Corre dentro de la unidad de trabajo del llamador, así que el evento y su
/// alerta se confirman juntos; las implementaciones no deben guardar cambios por su cuenta.
/// </summary>
public interface IEvaluadorAlertas
{
    Task<Alerta?> EvaluarAsync(EventoPesaje evento, CancellationToken cancelacion = default);
}

/// <summary>Implementación provisoria hasta que el módulo Analítica registre el evaluador real.</summary>
public sealed class EvaluadorAlertasNulo : IEvaluadorAlertas
{
    public Task<Alerta?> EvaluarAsync(EventoPesaje evento, CancellationToken cancelacion = default) =>
        Task.FromResult<Alerta?>(null);
}
