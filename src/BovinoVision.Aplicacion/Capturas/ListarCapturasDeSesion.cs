using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Capturas;

public sealed record ListarCapturasDeSesionConsulta(Guid IdSesion);

public sealed class ListarCapturasDeSesion(IRepositorioSesionesCaptura sesiones, IConsultasCapturas consultas)
{
    public async Task<IReadOnlyList<CapturaDeSesionRespuesta>> EjecutarAsync(
        ListarCapturasDeSesionConsulta consulta,
        CancellationToken cancelacion = default)
    {
        _ = await sesiones.ObtenerAsync(consulta.IdSesion, cancelacion)
            ?? throw NoEncontradoError.De("la sesión de captura", consulta.IdSesion);

        return await consultas.ListarDeSesionAsync(consulta.IdSesion, cancelacion);
    }
}
