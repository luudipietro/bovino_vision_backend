using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Capturas;
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
}

internal sealed class CapturasEnMemoria : IRepositorioCapturas
{
    public List<Captura> Capturas { get; } = [];

    public Task<Captura?> ObtenerAsync(Guid id, CancellationToken cancelacion = default) =>
        Task.FromResult(Capturas.SingleOrDefault(c => c.Id == id));

    public void Agregar(Captura captura) => Capturas.Add(captura);
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

    public Task EliminarAsync(string referencia, CancellationToken cancelacion = default)
    {
        Archivos.Remove(referencia);
        return Task.CompletedTask;
    }
}
