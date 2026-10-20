using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Pesajes;

namespace BovinoVision.Aplicacion.Pesajes;

public sealed record TrabajoInferencia(Guid IdCaptura, int Intento, string ReferenciaImagen);
public sealed record ResultadoEstimacion(decimal? PesoKg, string VersionModelo, string? MotivoFallo = null);

/// <summary>Adapter boundary for the external vision service; transport failures are retryable.</summary>
public interface IEstimadorPeso
{
    Task<ResultadoEstimacion> EstimarAsync(string referenciaImagen, CancellationToken cancelacion = default);
}

/// <summary>Short row locks protect transitions; no lock is held while estimating.</summary>
public interface IColaInferencia
{
    Task<TrabajoInferencia?> ReclamarAsync(DateTimeOffset ahora, TimeSpan arrendamiento, int maximoIntentos, CancellationToken cancelacion);
    Task ConCapturaBloqueadaAsync(Guid id, Func<Captura, Task> accion, CancellationToken cancelacion);
    Task<Guid?> ResolverHistorialAsync(Guid idAnimal, DateTimeOffset fecha, CancellationToken cancelacion);
    void Agregar(EventoPesaje evento);
}

/// <summary>
/// Options pattern. Section Inferencia: background processing true, 3 attempts,
/// retry delay 30 seconds, lease/estimator timeout 120 seconds, polling every 1 second.
/// </summary>
public sealed class OpcionesInferencia
{
    public bool ProcesarEnSegundoPlano { get; set; } = true;
    public int MaximoIntentos { get; set; } = 3;
    public int DemoraReintentoSegundos { get; set; } = 30;
    public int ArrendamientoSegundos { get; set; } = 120;
    public int IntervaloSegundos { get; set; } = 1;
}
