using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Capturas;

public sealed record FinalizarSesionCapturaComando(Guid IdSesion);

public sealed class FinalizarSesionCaptura(
    IRepositorioSesionesCaptura sesiones,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider tiempo)
{
    public async Task<SesionCapturaRespuesta> EjecutarAsync(FinalizarSesionCapturaComando comando, CancellationToken cancelacion = default)
    {
        var sesion = await sesiones.ObtenerAsync(comando.IdSesion, cancelacion)
            ?? throw NoEncontradoError.De("la sesión de captura", comando.IdSesion);

        sesion.Finalizar(tiempo.GetUtcNow());
        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        return SesionCapturaRespuesta.Desde(sesion);
    }
}
