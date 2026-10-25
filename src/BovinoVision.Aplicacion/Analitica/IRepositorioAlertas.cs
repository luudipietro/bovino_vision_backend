using BovinoVision.Dominio.Analitica;

namespace BovinoVision.Aplicacion.Analitica;

/// <summary>Datos que lee el evaluador de alertas y dónde prepara las alertas nuevas (nunca se guardan acá).</summary>
public interface IRepositorioAlertas
{
    /// <summary>Último pesaje persistido del animal estrictamente anterior a la hora de captura dada.</summary>
    Task<Medicion?> ObtenerPesajeAnteriorAsync(Guid idAnimal, DateTimeOffset fechaPesaje, CancellationToken cancelacion = default);

    /// <summary>GMD esperada del lote referenciado por una asignación de lote; null cuando el lote no tiene.</summary>
    Task<decimal?> ObtenerGmdObjetivoAsync(Guid idHistorialLote, CancellationToken cancelacion = default);

    /// <summary>True cuando el evento ya tiene una alerta, persistida o preparada en la unidad de trabajo actual.</summary>
    Task<bool> ExisteParaEventoAsync(Guid idEvento, CancellationToken cancelacion = default);

    /// <summary>Alerta con seguimiento para actualizarla; null cuando no existe o pertenece a otro establecimiento.</summary>
    Task<Alerta?> ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion = default);

    void Agregar(Alerta alerta);
}
