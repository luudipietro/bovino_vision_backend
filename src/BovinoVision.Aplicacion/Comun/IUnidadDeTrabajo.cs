namespace BovinoVision.Aplicacion.Comun;

/// <summary>
/// Confirma en una sola transacción todos los cambios registrados durante un caso de uso.
/// Los repositorios solo preparan cambios; cada caso de uso llama a esto una vez al final.
/// </summary>
public interface IUnidadDeTrabajo
{
    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}
