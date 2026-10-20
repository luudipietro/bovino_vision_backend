using BovinoVision.Dominio.Analitica;

namespace BovinoVision.Aplicacion.Analitica;

/// <summary>Data the alert evaluator reads, and where it stages new alerts (never saved here).</summary>
public interface IRepositorioAlertas
{
    /// <summary>Latest persisted weighing of the animal strictly before the given capture time.</summary>
    Task<Medicion?> ObtenerPesajeAnteriorAsync(Guid idAnimal, DateTimeOffset fechaPesaje, CancellationToken cancelacion = default);

    /// <summary>Expected GMD of the lot referenced by a lot assignment; null when the lot has none.</summary>
    Task<decimal?> ObtenerGmdObjetivoAsync(Guid idHistorialLote, CancellationToken cancelacion = default);

    /// <summary>True when the event already has an alert, persisted or staged in the current unit of work.</summary>
    Task<bool> ExisteParaEventoAsync(Guid idEvento, CancellationToken cancelacion = default);

    void Agregar(Alerta alerta);
}
