using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Capturas;

namespace BovinoVision.Aplicacion.Capturas;

public sealed class IniciarSesionCaptura(
    IRepositorioSesionesCaptura sesiones,
    IUnidadDeTrabajo unidadDeTrabajo,
    IContextoUsuario usuario,
    TimeProvider tiempo)
{
    public async Task<SesionCapturaRespuesta> EjecutarAsync(CancellationToken cancelacion = default)
    {
        var sesion = new SesionCaptura(usuario.IdUsuario, tiempo.GetUtcNow());
        sesiones.Agregar(sesion);
        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        return SesionCapturaRespuesta.Desde(sesion);
    }
}
