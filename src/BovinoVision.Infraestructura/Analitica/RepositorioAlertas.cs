using BovinoVision.Aplicacion.Analitica;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace BovinoVision.Infraestructura.Analitica;

internal sealed class RepositorioAlertas(BovinoVisionDbContext contexto) : IRepositorioAlertas
{
    public async Task<Medicion?> ObtenerPesajeAnteriorAsync(
        Guid idAnimal, DateTimeOffset fechaPesaje, CancellationToken cancelacion = default)
    {
        var anterior = await contexto.EventosPesaje
            .AsNoTracking()
            .Where(e => e.IdAnimal == idAnimal && e.FechaPesaje < fechaPesaje)
            .OrderByDescending(e => e.FechaPesaje)
            .Select(e => new { e.PesoKg, e.FechaPesaje })
            .FirstOrDefaultAsync(cancelacion);

        return anterior is null ? null : new Medicion(anterior.PesoKg, anterior.FechaPesaje);
    }

    public Task<decimal?> ObtenerGmdObjetivoAsync(Guid idHistorialLote, CancellationToken cancelacion = default) =>
        (from historial in contexto.HistorialLotes
         join lote in contexto.Lotes on historial.IdLote equals lote.Id
         where historial.Id == idHistorialLote
         select lote.GmdObjetivo)
        .FirstOrDefaultAsync(cancelacion);

    public async Task<bool> ExisteParaEventoAsync(Guid idEvento, CancellationToken cancelacion = default) =>
        contexto.Alertas.Local.Any(a => a.IdEvento == idEvento)
        || await contexto.Alertas.AnyAsync(a => a.IdEvento == idEvento, cancelacion);

    public void Agregar(Alerta alerta) => contexto.Alertas.Add(alerta);
}
