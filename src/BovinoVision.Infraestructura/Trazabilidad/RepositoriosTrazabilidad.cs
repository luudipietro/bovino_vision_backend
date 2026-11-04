using BovinoVision.Aplicacion.Trazabilidad;
using BovinoVision.Dominio.Trazabilidad;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace BovinoVision.Infraestructura.Trazabilidad;

internal sealed class RepositorioLotes(BovinoVisionDbContext contexto) : IRepositorioLotes
{
    public Task<Lote?> ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion) =>
        contexto.Lotes.SingleOrDefaultAsync(l => l.Id == id && l.IdEstablecimiento == idEstablecimiento, cancelacion);

    public Task<bool> ExisteCodigoAsync(Guid idEstablecimiento, string codigo, CancellationToken cancelacion) =>
        contexto.Lotes.AnyAsync(l => l.IdEstablecimiento == idEstablecimiento && l.Codigo == codigo, cancelacion);

    public void Agregar(Lote lote) => contexto.Lotes.Add(lote);

    public void Renombrar(Lote lote, string nombre) =>
        contexto.Entry(lote).Property(l => l.Nombre).CurrentValue = nombre;
}

internal sealed class RepositorioAnimales(BovinoVisionDbContext contexto) : IRepositorioAnimales
{
    private readonly HashSet<Guid> _asignacionesCargadas = [];

    public async Task<Animal?> ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion)
    {
        var animal = await contexto.Animales
            .Include(a => a.HistorialLotes)
            .SingleOrDefaultAsync(a => a.Id == id && a.IdEstablecimiento == idEstablecimiento, cancelacion);

        _asignacionesCargadas.UnionWith(animal?.HistorialLotes.Select(h => h.Id) ?? []);
        return animal;
    }

    public Task<bool> ExisteIdentificadorAsync(Guid idEstablecimiento, string identificador, CancellationToken cancelacion) =>
        contexto.Animales.AnyAsync(a => a.IdEstablecimiento == idEstablecimiento && a.Identificador == identificador, cancelacion);

    public void Agregar(Animal animal) => contexto.Animales.Add(animal);

    public void Actualizar(Animal animal)
    {
        // Las claves se generan en el cliente, así que la detección de cambios trata una asignación alcanzada
        // por la navegación como una fila existente (Modified). Todo lo que no se cargó es nuevo.
        foreach (var asignacion in animal.HistorialLotes.Where(h => !_asignacionesCargadas.Contains(h.Id)))
        {
            contexto.Entry(asignacion).State = EntityState.Added;
        }
    }
}

internal sealed class CatalogoRazas(BovinoVisionDbContext contexto) : ICatalogoRazas
{
    public Task<bool> ExisteAsync(Guid id, CancellationToken cancelacion) =>
        contexto.Razas.AnyAsync(r => r.Id == id, cancelacion);
}
