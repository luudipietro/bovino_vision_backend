using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Capturas;

public sealed record ObtenerCapturaConsulta(Guid IdCaptura);

public sealed class ObtenerCaptura(IConsultasCapturas consultas)
{
    public async Task<CapturaRespuesta> EjecutarAsync(ObtenerCapturaConsulta consulta, CancellationToken cancelacion = default) =>
        await consultas.ObtenerCapturaAsync(consulta.IdCaptura, cancelacion)
            ?? throw NoEncontradoError.De("la captura", consulta.IdCaptura);
}
