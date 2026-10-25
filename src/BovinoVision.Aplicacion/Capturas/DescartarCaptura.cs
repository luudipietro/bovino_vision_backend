using BovinoVision.Aplicacion.Comun;
using BovinoVision.Aplicacion.Pesajes;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Capturas;

public sealed record DescartarCapturaComando(Guid IdCaptura);

/// <summary>Elimina una captura registrada y su foto cuando el operario la vuelve a tomar.</summary>
public sealed class DescartarCaptura(
    IColaInferencia cola,
    IRepositorioCapturas capturas,
    IAnimalesDeCaptura animales,
    IAlmacenamientoImagenes almacenamiento,
    IUnidadDeTrabajo unidadDeTrabajo,
    IContextoUsuario usuario)
{
    public async Task EjecutarAsync(DescartarCapturaComando comando, CancellationToken cancelacion = default)
    {
        IReadOnlyList<string> referencias = [];

        // El bloqueo de fila evita que una solicitud de estimación concurrente compita con el descarte.
        await cola.ConCapturaBloqueadaAsync(comando.IdCaptura, async captura =>
        {
            if (!await animales.ExisteAsync(captura.IdAnimal, usuario.IdEstablecimiento, cancelacion))
            {
                throw NoEncontradoError.De("la captura", comando.IdCaptura);
            }

            referencias = captura.Descartar();
            capturas.Eliminar(captura);
            await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        }, cancelacion);

        // Los archivos se borran solo después de guardar el cambio en la base: un archivo sobrante es inofensivo, una referencia colgante no.
        foreach (var referencia in referencias)
        {
            await almacenamiento.EliminarAsync(referencia, CancellationToken.None);
        }
    }
}
