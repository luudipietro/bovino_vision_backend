using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Analitica;

public sealed record ConsultaHistorialPesajes(Guid IdAnimal);

public sealed record PesajeHistorialRespuesta(
    Guid Id,
    decimal PesoKg,
    DateTimeOffset FechaPesaje,
    DateTimeOffset FechaProcesamiento,
    string VersionModelo,
    Guid IdImagenUtilizada,
    string? LoteCodigo,
    decimal? GmdKgDia,
    string? Operario);

/// <summary>Historial de pesajes de un animal, del más reciente al más antiguo, con la GMD respecto del pesaje anterior.</summary>
public sealed class ConsultarHistorialPesajes(IConsultasAnalitica consultas, IContextoUsuario usuario, CalculadoraGmd calculadora)
{
    public async Task<IReadOnlyList<PesajeHistorialRespuesta>> EjecutarAsync(
        ConsultaHistorialPesajes consulta, CancellationToken cancelacion = default)
    {
        await ValidacionesAnalitica.ExigirAnimalAsync(consultas, usuario, consulta.IdAnimal, cancelacion);

        var pesajes = (await consultas.ListarPesajesDeAnimalAsync(consulta.IdAnimal, cancelacion))
            .OrderBy(p => p.FechaPesaje)
            .ToList();

        var historial = new List<PesajeHistorialRespuesta>(pesajes.Count);
        PesajeDeAnimal? anterior = null;
        foreach (var pesaje in pesajes)
        {
            var gmd = anterior is null
                ? null
                : calculadora.Calcular(new Medicion(anterior.PesoKg, anterior.FechaPesaje), new Medicion(pesaje.PesoKg, pesaje.FechaPesaje));
            historial.Add(new PesajeHistorialRespuesta(
                pesaje.Id, pesaje.PesoKg, pesaje.FechaPesaje, pesaje.FechaProcesamiento, pesaje.VersionModelo,
                pesaje.IdImagenUtilizada, pesaje.LoteCodigo, gmd, pesaje.Operario));
            anterior = pesaje;
        }

        historial.Reverse();
        return historial;
    }
}

internal static class ValidacionesAnalitica
{
    public static async Task ExigirAnimalAsync(
        IConsultasAnalitica consultas, IContextoUsuario usuario, Guid idAnimal, CancellationToken cancelacion)
    {
        if (!await consultas.ExisteAnimalAsync(usuario.IdEstablecimiento, idAnimal, cancelacion))
        {
            throw NoEncontradoError.De("el animal", idAnimal);
        }
    }
}
