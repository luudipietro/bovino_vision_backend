using BovinoVision.Aplicacion.Comun;

namespace BovinoVision.Aplicacion.Analitica;

public sealed record ConsultaAlertasDeAnimal(Guid IdAnimal);

/// <summary>Every alert of an animal (active or resolved), newest first.</summary>
public sealed class ListarAlertasDeAnimal(IConsultasAnalitica consultas, IContextoUsuario usuario)
{
    public async Task<IReadOnlyList<AlertaRespuesta>> EjecutarAsync(
        ConsultaAlertasDeAnimal consulta, CancellationToken cancelacion = default)
    {
        await ValidacionesAnalitica.ExigirAnimalAsync(consultas, usuario, consulta.IdAnimal, cancelacion);

        var alertas = await consultas.ListarAlertasAsync(usuario.IdEstablecimiento, consulta.IdAnimal, soloActivas: false, cancelacion);
        return alertas.OrderByDescending(a => a.FechaGeneracion).ToList();
    }
}
