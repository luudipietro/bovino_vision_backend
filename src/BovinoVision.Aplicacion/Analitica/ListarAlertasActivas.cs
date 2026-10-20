using BovinoVision.Aplicacion.Comun;

namespace BovinoVision.Aplicacion.Analitica;

/// <summary>Active alerts of the establecimiento: most urgent first, then newest first.</summary>
public sealed class ListarAlertasActivas(IConsultasAnalitica consultas, IContextoUsuario usuario)
{
    public async Task<IReadOnlyList<AlertaRespuesta>> EjecutarAsync(CancellationToken cancelacion = default)
    {
        var alertas = await consultas.ListarAlertasAsync(usuario.IdEstablecimiento, idAnimal: null, soloActivas: true, cancelacion);
        return alertas.OrderBy(a => a.Prioridad).ThenByDescending(a => a.FechaGeneracion).ToList();
    }
}
