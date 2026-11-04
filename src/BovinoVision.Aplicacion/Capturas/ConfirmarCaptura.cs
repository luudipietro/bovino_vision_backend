using BovinoVision.Aplicacion.Comun;
using BovinoVision.Aplicacion.Pesajes;

namespace BovinoVision.Aplicacion.Capturas;

public sealed record ConfirmarCapturaComando(Guid IdCaptura);

/// <summary>Solo registra la confirmación; los eventos de pesaje siguen siendo inmutables.</summary>
public sealed class ConfirmarCaptura(IColaInferencia cola, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider tiempo)
{
    public Task EjecutarAsync(ConfirmarCapturaComando comando, CancellationToken cancelacion = default) =>
        cola.ConCapturaBloqueadaAsync(comando.IdCaptura, async captura =>
        {
            captura.RegistrarRevision(tiempo.GetUtcNow());
            await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        }, cancelacion);
}
