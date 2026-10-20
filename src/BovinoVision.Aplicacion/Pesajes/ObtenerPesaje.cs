using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Pesajes;

public sealed record PesoPrevioRespuesta(decimal PesoKg, DateTimeOffset FechaPesaje);
public sealed record PesajeRespuesta(Guid Id, Guid IdAnimal, Guid IdCaptura, string Estado, decimal PesoKg,
    DateTimeOffset FechaPesaje, string VersionModelo, string AnimalIdentificador, string? LoteCodigo,
    PesoPrevioRespuesta? UltimoPesoPrevio, Alerta? Alerta);

public interface IConsultasPesajes
{
    Task<PesajeRespuesta?> ObtenerAsync(Guid id, CancellationToken cancelacion = default);
}

public sealed class ObtenerPesaje(IConsultasPesajes consultas)
{
    public async Task<PesajeRespuesta> EjecutarAsync(Guid id, CancellationToken cancelacion = default) =>
        await consultas.ObtenerAsync(id, cancelacion) ?? throw NoEncontradoError.De("el evento de pesaje", id);
}
