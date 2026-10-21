using BovinoVision.Aplicacion.Comun;
using BovinoVision.Aplicacion.Pesajes;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Capturas;

public sealed record DescartarCapturaComando(Guid IdCaptura);

/// <summary>Removes a registered capture and its photo when the operator retakes it.</summary>
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

        // The row lock keeps a concurrent estimation request from racing with the discard.
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

        // Files go only after the database change is saved: a leftover file is harmless, a dangling reference is not.
        foreach (var referencia in referencias)
        {
            await almacenamiento.EliminarAsync(referencia, CancellationToken.None);
        }
    }
}
