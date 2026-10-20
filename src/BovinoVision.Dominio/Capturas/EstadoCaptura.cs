namespace BovinoVision.Dominio.Capturas;

/// <summary>
/// Lifecycle of a capture, which doubles as the inference work queue:
/// registrada → pendiente → procesando → procesada | fallida.
/// A transient estimator failure returns the capture to pendiente.
/// </summary>
public enum EstadoCaptura
{
    Registrada,
    Pendiente,
    Procesando,
    Procesada,
    Fallida,
}
