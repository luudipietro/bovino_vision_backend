using BovinoVision.Dominio.Trazabilidad;

namespace BovinoVision.Aplicacion.Trazabilidad;

/// <summary>Lado de escritura de los lotes; las implementaciones solo preparan cambios.</summary>
public interface IRepositorioLotes
{
    Task<Lote?> ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion);

    Task<bool> ExisteCodigoAsync(Guid idEstablecimiento, string codigo, CancellationToken cancelacion);

    void Agregar(Lote lote);

    /// <summary>Reemplazo provisorio hasta que <see cref="Lote"/> exponga una operación para renombrar.</summary>
    void Renombrar(Lote lote, string nombre);
}

/// <summary>Lado de escritura de los animales; las implementaciones solo preparan cambios.</summary>
public interface IRepositorioAnimales
{
    /// <summary>Devuelve el animal con todo su historial de lotes, o null cuando no está en el establecimiento.</summary>
    Task<Animal?> ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion);

    Task<bool> ExisteIdentificadorAsync(Guid idEstablecimiento, string identificador, CancellationToken cancelacion);

    void Agregar(Animal animal);

    /// <summary>Prepara los cambios hechos a un animal devuelto por <see cref="ObtenerAsync"/>, incluidas las nuevas asignaciones de lote.</summary>
    void Actualizar(Animal animal);
}

public interface ICatalogoRazas
{
    Task<bool> ExisteAsync(Guid id, CancellationToken cancelacion);
}

public sealed record FiltroAnimales(Guid? IdLote = null, string? Busqueda = null, bool SoloConAlertasActivas = false);

/// <summary>Lado de lectura del módulo; cada método ejecuta una cantidad fija de consultas.</summary>
public interface IConsultasTrazabilidad
{
    Task<IReadOnlyList<RazaRespuesta>> ListarRazasAsync(CancellationToken cancelacion);

    Task<IReadOnlyList<LoteRespuesta>> ListarLotesAsync(Guid idEstablecimiento, CancellationToken cancelacion);

    Task<LoteRespuesta?> ObtenerLoteAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion);

    Task<IReadOnlyList<AnimalRespuesta>> ListarAnimalesAsync(Guid idEstablecimiento, FiltroAnimales filtro, CancellationToken cancelacion);

    Task<AnimalRespuesta?> ObtenerAnimalAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion);

    /// <summary>Asignaciones de lote de la más reciente a la más antigua, o null cuando el animal no está en el establecimiento.</summary>
    Task<IReadOnlyList<AsignacionLoteRespuesta>?> ListarHistorialLotesAsync(Guid idAnimal, Guid idEstablecimiento, CancellationToken cancelacion);
}
