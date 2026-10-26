using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Pesajes;

namespace BovinoVision.Aplicacion.Pesajes;

/// <summary>El intento identifica el pedido al servicio externo: cada reintento es un trabajo nuevo.</summary>
public sealed record TrabajoInferencia(Guid IdCaptura, int Intento, string ReferenciaImagen, string TipoContenido);

/// <summary>Sin peso, <see cref="MotivoFallo"/> explica la falla y <see cref="Reintentable"/> indica si vale otro intento.</summary>
public sealed record ResultadoEstimacion(decimal? PesoKg, string VersionModelo, string? MotivoFallo = null, bool Reintentable = false);

/// <summary>Frontera del adaptador para el servicio de visión externo; las fallas de transporte se pueden reintentar.</summary>
public interface IEstimadorPeso
{
    Task<ResultadoEstimacion> EstimarAsync(TrabajoInferencia trabajo, CancellationToken cancelacion = default);
}

/// <summary>Bloqueos de fila cortos protegen las transiciones; no se mantiene ningún bloqueo mientras se estima.</summary>
public interface IColaInferencia
{
    Task<TrabajoInferencia?> ReclamarAsync(DateTimeOffset ahora, TimeSpan arrendamiento, int maximoIntentos, CancellationToken cancelacion);
    Task ConCapturaBloqueadaAsync(Guid id, Func<Captura, Task> accion, CancellationToken cancelacion);
    Task<Guid?> ResolverHistorialAsync(Guid idAnimal, DateTimeOffset fecha, CancellationToken cancelacion);
    void Agregar(EventoPesaje evento);
}

/// <summary>
/// Patrón Options. Sección Inferencia: procesamiento en segundo plano activo, 3 intentos,
/// espera de reintento de 30 segundos, vencimiento de la concesión y del estimador de 120 segundos, sondeo cada 1 segundo.
/// </summary>
public sealed class OpcionesInferencia
{
    public bool ProcesarEnSegundoPlano { get; set; } = true;
    public int MaximoIntentos { get; set; } = 3;
    public int DemoraReintentoSegundos { get; set; } = 30;
    public int ArrendamientoSegundos { get; set; } = 120;
    public int IntervaloSegundos { get; set; } = 1;
}
