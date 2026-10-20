using BovinoVision.Dominio.Trazabilidad;

namespace BovinoVision.Aplicacion.Trazabilidad;

/// <summary>Write side for lots; implementations only stage changes.</summary>
public interface IRepositorioLotes
{
    Task<Lote?> ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion);

    Task<bool> ExisteCodigoAsync(Guid idEstablecimiento, string codigo, CancellationToken cancelacion);

    void Agregar(Lote lote);

    /// <summary>Stand-in until <see cref="Lote"/> exposes a rename operation.</summary>
    void Renombrar(Lote lote, string nombre);
}

/// <summary>Write side for animals; implementations only stage changes.</summary>
public interface IRepositorioAnimales
{
    /// <summary>Returns the animal with its whole lot history, or null when it is not in the establecimiento.</summary>
    Task<Animal?> ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion);

    Task<bool> ExisteIdentificadorAsync(Guid idEstablecimiento, string identificador, CancellationToken cancelacion);

    void Agregar(Animal animal);

    /// <summary>Stages the changes made to an animal returned by <see cref="ObtenerAsync"/>, including new lot assignments.</summary>
    void Actualizar(Animal animal);
}

public interface ICatalogoRazas
{
    Task<bool> ExisteAsync(Guid id, CancellationToken cancelacion);
}

public sealed record FiltroAnimales(Guid? IdLote = null, string? Busqueda = null, bool SoloConAlertasActivas = false);

/// <summary>Read side of the module; every method runs a fixed number of queries.</summary>
public interface IConsultasTrazabilidad
{
    Task<IReadOnlyList<RazaRespuesta>> ListarRazasAsync(CancellationToken cancelacion);

    Task<IReadOnlyList<LoteRespuesta>> ListarLotesAsync(Guid idEstablecimiento, CancellationToken cancelacion);

    Task<LoteRespuesta?> ObtenerLoteAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion);

    Task<IReadOnlyList<AnimalRespuesta>> ListarAnimalesAsync(Guid idEstablecimiento, FiltroAnimales filtro, CancellationToken cancelacion);

    Task<AnimalRespuesta?> ObtenerAnimalAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion);

    /// <summary>Lot assignments newest first, or null when the animal is not in the establecimiento.</summary>
    Task<IReadOnlyList<AsignacionLoteRespuesta>?> ListarHistorialLotesAsync(Guid idAnimal, Guid idEstablecimiento, CancellationToken cancelacion);
}
