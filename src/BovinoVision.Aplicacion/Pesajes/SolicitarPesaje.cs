using BovinoVision.Aplicacion.Comun;

namespace BovinoVision.Aplicacion.Pesajes;

public sealed record SolicitarPesajeComando(Guid IdCaptura);

public sealed class SolicitarPesaje(IColaInferencia cola, IUnidadDeTrabajo unidadDeTrabajo)
{
    public Task EjecutarAsync(SolicitarPesajeComando comando, CancellationToken cancelacion = default) =>
        cola.ConCapturaBloqueadaAsync(comando.IdCaptura, async captura =>
        {
            captura.SolicitarEstimacion();
            await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        }, cancelacion);
}
