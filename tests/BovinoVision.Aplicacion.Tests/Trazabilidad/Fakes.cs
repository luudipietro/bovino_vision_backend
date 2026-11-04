using BovinoVision.Aplicacion.Comun;
using BovinoVision.Aplicacion.Trazabilidad;
using BovinoVision.Dominio.Trazabilidad;

namespace BovinoVision.Aplicacion.Tests.Trazabilidad;

internal sealed class ContextoFijo(Guid idEstablecimiento) : IContextoUsuario
{
    public Guid IdUsuario { get; } = Guid.CreateVersion7();

    public Guid IdEstablecimiento { get; } = idEstablecimiento;
}

internal sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora;
}

/// <summary>
/// Implementación en memoria de todos los puertos del módulo. Las entidades agregadas quedan pendientes
/// hasta que corre <see cref="GuardarCambiosAsync"/>, así las lecturas solo ven datos confirmados.
/// </summary>
internal sealed class TrazabilidadEnMemoria
    : IRepositorioLotes, IRepositorioAnimales, ICatalogoRazas, IConsultasTrazabilidad, IUnidadDeTrabajo
{
    private readonly List<Lote> _lotes = [];
    private readonly List<Animal> _animales = [];
    private readonly List<object> _pendientes = [];
    private readonly Dictionary<Guid, string> _nombresLote = [];

    public List<Raza> Razas { get; } = [new(Guid.CreateVersion7(), "Hereford"), new(Guid.CreateVersion7(), "Aberdeen Angus")];

    public int Guardados { get; private set; }

    public IReadOnlyList<Animal> Animales => _animales;

    public IReadOnlyList<Lote> Lotes => _lotes;

    public Lote AgregarLoteGuardado(Guid idEstablecimiento, string codigo, decimal? gmdObjetivo = null)
    {
        var lote = new Lote(idEstablecimiento, codigo, gmdObjetivo: gmdObjetivo);
        _lotes.Add(lote);
        return lote;
    }

    public Animal AgregarAnimalGuardado(Guid idEstablecimiento, string identificador)
    {
        var animal = new Animal(idEstablecimiento, identificador);
        _animales.Add(animal);
        return animal;
    }

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default)
    {
        _lotes.AddRange(_pendientes.OfType<Lote>());
        _animales.AddRange(_pendientes.OfType<Animal>());
        _pendientes.Clear();
        Guardados++;
        return Task.CompletedTask;
    }

    Task<Lote?> IRepositorioLotes.ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion) =>
        Task.FromResult(_lotes.SingleOrDefault(l => l.Id == id && l.IdEstablecimiento == idEstablecimiento));

    public Task<bool> ExisteCodigoAsync(Guid idEstablecimiento, string codigo, CancellationToken cancelacion) =>
        Task.FromResult(_lotes.Any(l => l.IdEstablecimiento == idEstablecimiento && l.Codigo == codigo));

    public void Agregar(Lote lote) => _pendientes.Add(lote);

    public void Renombrar(Lote lote, string nombre) => _nombresLote[lote.Id] = nombre;

    Task<Animal?> IRepositorioAnimales.ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion) =>
        Task.FromResult(_animales.SingleOrDefault(a => a.Id == id && a.IdEstablecimiento == idEstablecimiento));

    public Task<bool> ExisteIdentificadorAsync(Guid idEstablecimiento, string identificador, CancellationToken cancelacion) =>
        Task.FromResult(_animales.Any(a => a.IdEstablecimiento == idEstablecimiento && a.Identificador == identificador));

    public void Agregar(Animal animal) => _pendientes.Add(animal);

    public void Actualizar(Animal animal)
    {
        // Seguido por referencia: los cambios ya son visibles.
    }

    public Task<bool> ExisteAsync(Guid id, CancellationToken cancelacion) =>
        Task.FromResult(Razas.Any(r => r.Id == id));

    public Task<IReadOnlyList<RazaRespuesta>> ListarRazasAsync(CancellationToken cancelacion) =>
        Task.FromResult<IReadOnlyList<RazaRespuesta>>(
            [.. Razas.OrderBy(r => r.Nombre, StringComparer.Ordinal).Select(r => new RazaRespuesta(r.Id, r.Nombre))]);

    public Task<IReadOnlyList<LoteRespuesta>> ListarLotesAsync(Guid idEstablecimiento, CancellationToken cancelacion) =>
        Task.FromResult<IReadOnlyList<LoteRespuesta>>(
            [.. _lotes.Where(l => l.IdEstablecimiento == idEstablecimiento).OrderBy(l => l.Codigo, StringComparer.Ordinal).Select(ALoteRespuesta)]);

    public Task<LoteRespuesta?> ObtenerLoteAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion) =>
        Task.FromResult(_lotes.Where(l => l.Id == id && l.IdEstablecimiento == idEstablecimiento).Select(ALoteRespuesta).SingleOrDefault());

    public Task<IReadOnlyList<AnimalRespuesta>> ListarAnimalesAsync(Guid idEstablecimiento, FiltroAnimales filtro, CancellationToken cancelacion)
    {
        var animales = _animales.Where(a => a.IdEstablecimiento == idEstablecimiento);
        if (filtro.IdLote is { } idLote)
        {
            animales = animales.Where(a => a.AsignacionVigente?.IdLote == idLote);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            animales = animales.Where(a => a.Identificador.Contains(filtro.Busqueda, StringComparison.OrdinalIgnoreCase));
        }

        // No existen pesajes ni alertas en memoria.
        if (filtro.SoloConAlertasActivas)
        {
            animales = [];
        }

        return Task.FromResult<IReadOnlyList<AnimalRespuesta>>(
            [.. animales.OrderBy(a => a.Identificador, StringComparer.Ordinal).Select(AAnimalRespuesta)]);
    }

    public Task<AnimalRespuesta?> ObtenerAnimalAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion) =>
        Task.FromResult(_animales.Where(a => a.Id == id && a.IdEstablecimiento == idEstablecimiento).Select(AAnimalRespuesta).SingleOrDefault());

    public Task<IReadOnlyList<AsignacionLoteRespuesta>?> ListarHistorialLotesAsync(Guid idAnimal, Guid idEstablecimiento, CancellationToken cancelacion)
    {
        var animal = _animales.SingleOrDefault(a => a.Id == idAnimal && a.IdEstablecimiento == idEstablecimiento);
        IReadOnlyList<AsignacionLoteRespuesta>? historial = animal?.HistorialLotes
            .OrderByDescending(h => h.VigenteDesde)
            .Select(h => new AsignacionLoteRespuesta(h.IdLote, CodigoDe(h.IdLote)!, h.VigenteDesde, h.VigenteHasta))
            .ToList();
        return Task.FromResult(historial);
    }

    public string? NombreDe(Lote lote) => _nombresLote.GetValueOrDefault(lote.Id, lote.Nombre!);

    private LoteRespuesta ALoteRespuesta(Lote lote) => new(
        lote.Id,
        lote.Codigo,
        NombreDe(lote),
        lote.GmdObjetivo,
        _animales.Count(a => a.AsignacionVigente?.IdLote == lote.Id));

    private AnimalRespuesta AAnimalRespuesta(Animal animal)
    {
        var idLote = animal.AsignacionVigente?.IdLote;
        return new AnimalRespuesta(
            animal.Id,
            animal.Identificador,
            animal.IdEstablecimiento,
            idLote,
            idLote is { } id ? CodigoDe(id) : null,
            animal.IdRaza,
            Razas.SingleOrDefault(r => r.Id == animal.IdRaza)?.Nombre,
            animal.Sexo,
            animal.FechaNacimiento,
            null,
            0,
            null,
            null);
    }

    private string? CodigoDe(Guid idLote) => _lotes.SingleOrDefault(l => l.Id == idLote)?.Codigo;
}
