using BovinoVision.Aplicacion.Comun;
using BovinoVision.Aplicacion.Pesajes;

namespace BovinoVision.Aplicacion.Capturas;

public sealed record ConfirmarCapturaComando(Guid IdCaptura);

/// <summary>Records acknowledgment only; weighing events remain immutable.</summary>
public sealed class ConfirmarCaptura(IColaInferencia cola, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider tiempo)
{
    public Task EjecutarAsync(ConfirmarCapturaComando comando, CancellationToken cancelacion = default) =>
        cola.ConCapturaBloqueadaAsync(comando.IdCaptura, async captura =>
        {
            captura.RegistrarRevision(tiempo.GetUtcNow());
            await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        }, cancelacion);
}
