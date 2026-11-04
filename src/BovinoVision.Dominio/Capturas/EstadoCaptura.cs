namespace BovinoVision.Dominio.Capturas;

/// <summary>
/// Ciclo de vida de una captura, que además funciona como cola de trabajo de inferencia:
/// registrada → pendiente → procesando → procesada | fallida.
/// Una falla transitoria del estimador devuelve la captura a pendiente.
/// </summary>
public enum EstadoCaptura
{
    Registrada,
    Pendiente,
    Procesando,
    Procesada,
    Fallida,
}
