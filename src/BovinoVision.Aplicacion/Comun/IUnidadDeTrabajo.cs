namespace BovinoVision.Aplicacion.Comun;

/// <summary>
/// Commits every change tracked during a use case in a single transaction.
/// Repositories only stage changes; each use case calls this once at the end.
/// </summary>
public interface IUnidadDeTrabajo
{
    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}
