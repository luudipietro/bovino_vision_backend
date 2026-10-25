using BovinoVision.Dominio.Analitica;

namespace BovinoVision.Aplicacion.Analitica;

/// <summary>Consultas de solo lectura del módulo Analítica; cada método hace un único viaje a la base de datos.</summary>
public interface IConsultasAnalitica
{
    Task<bool> ExisteAnimalAsync(Guid idEstablecimiento, Guid idAnimal, CancellationToken cancelacion = default);

    /// <summary>Todos los pesajes del animal con el código del lote al que pertenecía en esa fecha.</summary>
    Task<IReadOnlyList<PesajeDeAnimal>> ListarPesajesDeAnimalAsync(Guid idAnimal, CancellationToken cancelacion = default);

    Task<IReadOnlyList<AlertaRespuesta>> ListarAlertasAsync(
        Guid idEstablecimiento, Guid? idAnimal, bool soloActivas, CancellationToken cancelacion = default);

    Task<IReadOnlyList<PesajeResumido>> ListarPesajesAsync(FiltroPesajes filtro, CancellationToken cancelacion = default);
}

/// <param name="IdLote">Conserva solo los pesajes cuya asignación de lote en esa fecha pertenece a este lote.</param>
/// <param name="Desde">Límite inferior inclusivo de la hora de captura.</param>
/// <param name="Hasta">Límite superior exclusivo de la hora de captura.</param>
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
