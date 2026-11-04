using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Aplicacion.Comun;
using BovinoVision.Aplicacion.Pesajes;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Pesajes;
using BovinoVision.Dominio.Trazabilidad;

namespace BovinoVision.Aplicacion.Tests.Capturas;

internal sealed class TiempoControlado(DateTimeOffset ahora) : TimeProvider
{
    public DateTimeOffset Ahora { get; set; } = ahora;

    public override DateTimeOffset GetUtcNow() => Ahora;
}

internal sealed class UnidadDeTrabajoContadora : IUnidadDeTrabajo
{
    public int Guardados { get; private set; }

    public Exception? FallarCon { get; set; }

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default)
    {
        if (FallarCon is not null)
        {
            throw FallarCon;
        }

        Guardados++;
        return Task.CompletedTask;
    }
}

internal sealed class ContextoUsuarioFijo : IContextoUsuario
{
    public Guid IdUsuario { get; } = Guid.CreateVersion7();

    public Guid IdEstablecimiento { get; } = Guid.CreateVersion7();
}

internal sealed class SesionesEnMemoria : IRepositorioSesionesCaptura
{
    public List<SesionCaptura> Sesiones { get; } = [];

    public Task<SesionCaptura?> ObtenerAsync(Guid id, CancellationToken cancelacion = default) =>
        Task.FromResult(Sesiones.SingleOrDefault(s => s.Id == id));

    public void Agregar(SesionCaptura sesion) => Sesiones.Add(sesion);

    /// <summary>Fecha de la última captura de cada sesión; las ausentes no tienen capturas.</summary>
    public Dictionary<Guid, DateTimeOffset> UltimaCaptura { get; } = [];

    public DateTimeOffset? UltimoLimiteConsultado { get; private set; }

    public Task<IReadOnlyList<SesionInactiva>> ListarInactivasAsync(DateTimeOffset sinActividadDesde, CancellationToken cancelacion = default)
    {
        UltimoLimiteConsultado = sinActividadDesde;
        IReadOnlyList<SesionInactiva> inactivas = Sesiones
            .Where(s => s.Estado == EstadoSesionCaptura.Activa)
            .Select(s => new SesionInactiva(
                s, UltimaCaptura.TryGetValue(s.Id, out var ultima) && ultima > s.FechaInicio ? ultima : s.FechaInicio))
            .Where(s => s.UltimaActividad < sinActividadDesde)
            .ToList();
        return Task.FromResult(inactivas);
    }
}

internal sealed class CapturasEnMemoria : IRepositorioCapturas
{
    public List<Captura> Capturas { get; } = [];

    public Task<Captura?> ObtenerAsync(Guid id, CancellationToken cancelacion = default) =>
        Task.FromResult(Capturas.SingleOrDefault(c => c.Id == id));

    public void Agregar(Captura captura) => Capturas.Add(captura);

    public void Eliminar(Captura captura) => Capturas.Remove(captura);
}

/// <summary>Los casos de uso de capturas solo usan la operación con bloqueo de fila; ejecuta la acción sobre la captura almacenada.</summary>
internal sealed class ColaInferenciaEnMemoria(CapturasEnMemoria capturas) : IColaInferencia
{
    public Task ConCapturaBloqueadaAsync(Guid id, Func<Captura, Task> accion, CancellationToken cancelacion)
    {
        var captura = capturas.Capturas.SingleOrDefault(c => c.Id == id)
            ?? throw NoEncontradoError.De("la captura", id);
        return accion(captura);
    }

    public Task<TrabajoInferencia?> ReclamarAsync(DateTimeOffset ahora, TimeSpan arrendamiento, int maximoIntentos, CancellationToken cancelacion) =>
        throw new NotSupportedException();

    public Task<Guid?> ResolverHistorialAsync(Guid idAnimal, DateTimeOffset fecha, CancellationToken cancelacion) =>
        throw new NotSupportedException();

    public void Agregar(EventoPesaje evento) => throw new NotSupportedException();
}

internal sealed class AnimalesEnMemoria : IAnimalesDeCaptura
{
    public List<Animal> Animales { get; } = [];

    public Task<bool> ExisteAsync(Guid idAnimal, Guid idEstablecimiento, CancellationToken cancelacion = default) =>
        Task.FromResult(Animales.Any(a => a.Id == idAnimal && a.IdEstablecimiento == idEstablecimiento));
}

internal sealed class AlmacenamientoEnMemoria : IAlmacenamientoImagenes
{
    public Dictionary<string, byte[]> Archivos { get; } = [];

    public Task<string> GuardarAsync(string nombreSugerido, ReadOnlyMemory<byte> contenido, CancellationToken cancelacion = default)
    {
        var referencia = $"memoria/{Guid.NewGuid():N}{Path.GetExtension(nombreSugerido)}";
        Archivos[referencia] = contenido.ToArray();
        return Task.FromResult(referencia);
    }

    public Task<Stream?> AbrirAsync(string referencia, CancellationToken cancelacion = default) =>
        Task.FromResult<Stream?>(Archivos.TryGetValue(referencia, out var contenido) ? new MemoryStream(contenido) : null);

    public Task EliminarAsync(string referencia, CancellationToken cancelacion = default)
    {
        Archivos.Remove(referencia);
        return Task.CompletedTask;
    }
}

/// <summary>Los casos de uso bajo prueba solo usan la búsqueda de imágenes; registra el establecimiento por el que se preguntó.</summary>
internal sealed class ConsultasCapturasEnMemoria : IConsultasCapturas
{
    public Dictionary<Guid, (ImagenAlmacenada Imagen, Guid IdEstablecimiento)> Imagenes { get; } = [];

    public Guid? EstablecimientoConsultado { get; private set; }

    public Task<ImagenAlmacenada?> ObtenerImagenAsync(Guid idImagen, Guid idEstablecimiento, CancellationToken cancelacion = default)
    {
        EstablecimientoConsultado = idEstablecimiento;
        return Task.FromResult(Imagenes.TryGetValue(idImagen, out var fila) && fila.IdEstablecimiento == idEstablecimiento ? fila.Imagen : null);
    }

    public Task<CapturaRespuesta?> ObtenerCapturaAsync(Guid idCaptura, CancellationToken cancelacion = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<CapturaDeSesionRespuesta>> ListarDeSesionAsync(Guid idSesion, CancellationToken cancelacion = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<SesionRecienteRespuesta>> ListarSesionesRecientesAsync(
        Guid idUsuario, int limite, int desplazamiento,
        DateTimeOffset? desde = null, DateTimeOffset? hasta = null, CancellationToken cancelacion = default) =>
        throw new NotSupportedException();
}
