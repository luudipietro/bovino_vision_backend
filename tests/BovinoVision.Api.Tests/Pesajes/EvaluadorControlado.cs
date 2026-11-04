using BovinoVision.Aplicacion.Analitica;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Pesajes;
using BovinoVision.Infraestructura.Persistencia;

namespace BovinoVision.Api.Tests.Pesajes;

internal sealed class ControlAlertas
{
    public bool Fallar { get; set; }
    public int Llamadas { get; set; }
}

internal sealed class EvaluadorControlado(BovinoVisionDbContext contexto, ControlAlertas control) : IEvaluadorAlertas
{
    public Task<Alerta?> EvaluarAsync(EventoPesaje evento, CancellationToken cancelacion = default)
    {
        control.Llamadas++;
        var alerta = new Alerta(evento.Id, TipoAlerta.PerdidaPeso, 1, -1, null, "prueba-v1", evento.FechaProcesamiento);
        contexto.Alertas.Add(alerta);
        if (control.Fallar) { throw new InvalidOperationException("Fallo de evaluación simulado"); }
        return Task.FromResult<Alerta?>(alerta);
    }
}
