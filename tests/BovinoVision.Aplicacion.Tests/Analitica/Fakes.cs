using BovinoVision.Aplicacion.Analitica;
using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Pesajes;

namespace BovinoVision.Aplicacion.Tests.Analitica;

internal static class Prueba
{
    public static readonly TimeZoneInfo UtcMenos3 =
        TimeZoneInfo.CreateCustomTimeZone("UTC-3", TimeSpan.FromHours(-3), "UTC-3", "UTC-3");

    public static readonly DateTimeOffset Inicio = new(2026, 1, 1, 13, 0, 0, TimeSpan.Zero);

    public static CalculadoraGmd Calculadora() => new(UtcMenos3);

    public static EventoPesaje Evento(Guid idAnimal, decimal pesoKg, DateTimeOffset fecha, Guid? idHistorialLote = null)
    {
        var captura = new Captura(idAnimal, Guid.CreateVersion7(), Guid.CreateVersion7(), fecha);
        var imagen = captura.AgregarImagen("ref.jpg", "hash", "image/jpeg");
        return EventoPesaje.Registrar(captura, imagen.Id, idHistorialLote, pesoKg, fecha.AddMinutes(1), "fake-v1");
    }
}

internal sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora;
}

internal sealed class ContextoUsuarioFijo : IContextoUsuario
{
    public Guid IdUsuario { get; } = Guid.CreateVersion7();

    public Guid IdEstablecimiento { get; } = Guid.CreateVersion7();
}

/// <summary>Persisted events and alerts in memory; staged alerts are kept apart, as a unit of work would.</summary>
internal sealed class RepositorioAlertasEnMemoria : IRepositorioAlertas
{
    public List<EventoPesaje> Eventos { get; } = [];

    public Dictionary<Guid, decimal?> GmdObjetivoPorHistorial { get; } = [];

    public List<Alerta> Guardadas { get; } = [];

    public List<Alerta> Preparadas { get; } = [];

    public Task<Medicion?> ObtenerPesajeAnteriorAsync(Guid idAnimal, DateTimeOffset fechaPesaje, CancellationToken cancelacion = default)
    {
        var anterior = Eventos
            .Where(e => e.IdAnimal == idAnimal && e.FechaPesaje < fechaPesaje)
            .OrderByDescending(e => e.FechaPesaje)
            .Select(e => (Medicion?)new Medicion(e.PesoKg, e.FechaPesaje))
            .FirstOrDefault();
        return Task.FromResult(anterior);
    }

    public Task<decimal?> ObtenerGmdObjetivoAsync(Guid idHistorialLote, CancellationToken cancelacion = default) =>
        Task.FromResult(GmdObjetivoPorHistorial.GetValueOrDefault(idHistorialLote));

    public Task<bool> ExisteParaEventoAsync(Guid idEvento, CancellationToken cancelacion = default) =>
        Task.FromResult(Guardadas.Concat(Preparadas).Any(a => a.IdEvento == idEvento));

    public void Agregar(Alerta alerta) => Preparadas.Add(alerta);
}

/// <summary>Read model in memory; applies the same filters the database query applies.</summary>
internal sealed class ConsultasAnaliticaEnMemoria : IConsultasAnalitica
{
    public HashSet<(Guid IdEstablecimiento, Guid IdAnimal)> Animales { get; } = [];

    public List<(PesajeDeAnimal Pesaje, Guid IdAnimal)> PesajesDeAnimal { get; } = [];

    public List<(PesajeResumido Pesaje, Guid IdEstablecimiento, Guid? IdLote)> Pesajes { get; } = [];

    public List<(AlertaRespuesta Alerta, Guid IdEstablecimiento)> Alertas { get; } = [];

    public FiltroPesajes? UltimoFiltro { get; private set; }

    public Task<bool> ExisteAnimalAsync(Guid idEstablecimiento, Guid idAnimal, CancellationToken cancelacion = default) =>
        Task.FromResult(Animales.Contains((idEstablecimiento, idAnimal)));

    public Task<IReadOnlyList<PesajeDeAnimal>> ListarPesajesDeAnimalAsync(Guid idAnimal, CancellationToken cancelacion = default) =>
        Task.FromResult<IReadOnlyList<PesajeDeAnimal>>(
            PesajesDeAnimal.Where(p => p.IdAnimal == idAnimal).Select(p => p.Pesaje).ToList());

    public Task<IReadOnlyList<AlertaRespuesta>> ListarAlertasAsync(
        Guid idEstablecimiento, Guid? idAnimal, bool soloActivas, CancellationToken cancelacion = default) =>
        Task.FromResult<IReadOnlyList<AlertaRespuesta>>(Alertas
            .Where(a => a.IdEstablecimiento == idEstablecimiento)
            .Select(a => a.Alerta)
            .Where(a => idAnimal is null || a.IdAnimal == idAnimal)
            .Where(a => !soloActivas || a.Estado == EstadoAlerta.Activa)
            .ToList());

    public Task<IReadOnlyList<PesajeResumido>> ListarPesajesAsync(FiltroPesajes filtro, CancellationToken cancelacion = default)
    {
        UltimoFiltro = filtro;
        return Task.FromResult<IReadOnlyList<PesajeResumido>>(Pesajes
            .Where(p => p.IdEstablecimiento == filtro.IdEstablecimiento)
            .Where(p => filtro.IdLote is null || p.IdLote == filtro.IdLote)
            .Where(p => filtro.Desde is null || p.Pesaje.FechaPesaje >= filtro.Desde)
            .Where(p => filtro.Hasta is null || p.Pesaje.FechaPesaje < filtro.Hasta)
            .Select(p => p.Pesaje)
            .ToList());
    }
}
