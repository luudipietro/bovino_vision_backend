using BovinoVision.Dominio.Analitica;

namespace BovinoVision.Aplicacion.Analitica;

/// <summary>Read-only queries of the Analítica module; each method is a single database round trip.</summary>
public interface IConsultasAnalitica
{
    Task<bool> ExisteAnimalAsync(Guid idEstablecimiento, Guid idAnimal, CancellationToken cancelacion = default);

    /// <summary>Every weighing of the animal with the code of the lot it belonged to at that date.</summary>
    Task<IReadOnlyList<PesajeDeAnimal>> ListarPesajesDeAnimalAsync(Guid idAnimal, CancellationToken cancelacion = default);

    Task<IReadOnlyList<AlertaRespuesta>> ListarAlertasAsync(
        Guid idEstablecimiento, Guid? idAnimal, bool soloActivas, CancellationToken cancelacion = default);

    Task<IReadOnlyList<PesajeResumido>> ListarPesajesAsync(FiltroPesajes filtro, CancellationToken cancelacion = default);
}

/// <param name="IdLote">Keeps only weighings whose lot assignment at that date belongs to this lot.</param>
/// <param name="Desde">Inclusive lower bound of the capture time.</param>
/// <param name="Hasta">Exclusive upper bound of the capture time.</param>
public sealed record FiltroPesajes(Guid IdEstablecimiento, Guid? IdLote = null, DateTimeOffset? Desde = null, DateTimeOffset? Hasta = null);

public sealed record PesajeResumido(Guid IdAnimal, decimal PesoKg, DateTimeOffset FechaPesaje);

public sealed record PesajeDeAnimal(
    Guid Id,
    decimal PesoKg,
    DateTimeOffset FechaPesaje,
    DateTimeOffset FechaProcesamiento,
    string VersionModelo,
    Guid IdImagenUtilizada,
    string? LoteCodigo,
    string? Operario);

public sealed record AlertaRespuesta(
    Guid Id,
    Guid IdEvento,
    Guid IdAnimal,
    string AnimalIdentificador,
    TipoAlerta Tipo,
    int Prioridad,
    decimal Variacion,
    decimal? GmdEsperada,
    EstadoAlerta Estado,
    DateTimeOffset FechaGeneracion);
