using BovinoVision.Aplicacion.Analitica;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace BovinoVision.Infraestructura.Analitica;

/// <summary>Read-side adapter: projects batches without loading aggregates or issuing per-animal queries.</summary>
internal sealed class ConsultasAnalitica(BovinoVisionDbContext contexto) : IConsultasAnalitica
{
    public Task<bool> ExisteAnimalAsync(Guid idEstablecimiento, Guid idAnimal, CancellationToken cancelacion = default) =>
        contexto.Animales.AnyAsync(a => a.Id == idAnimal && a.IdEstablecimiento == idEstablecimiento, cancelacion);

    public async Task<IReadOnlyList<PesajeDeAnimal>> ListarPesajesDeAnimalAsync(
        Guid idAnimal, CancellationToken cancelacion = default) =>
        await (from evento in contexto.EventosPesaje.AsNoTracking()
               join historial in contexto.HistorialLotes on evento.IdHistorialLote equals historial.Id into historiales
               from historial in historiales.DefaultIfEmpty()
               join lote in contexto.Lotes on historial.IdLote equals lote.Id into lotes
               from lote in lotes.DefaultIfEmpty()
               where evento.IdAnimal == idAnimal
               orderby evento.FechaPesaje, evento.Id
               select new PesajeDeAnimal(
                   evento.Id, evento.PesoKg, evento.FechaPesaje, evento.FechaProcesamiento,
                   evento.VersionModelo, evento.IdImagenUtilizada, lote == null ? null : lote.Codigo))
            .ToListAsync(cancelacion);

    public async Task<IReadOnlyList<AlertaRespuesta>> ListarAlertasAsync(
        Guid idEstablecimiento, Guid? idAnimal, bool soloActivas, CancellationToken cancelacion = default) =>
        await (from alerta in contexto.Alertas.AsNoTracking()
               join evento in contexto.EventosPesaje on alerta.IdEvento equals evento.Id
               join animal in contexto.Animales on evento.IdAnimal equals animal.Id
               where animal.IdEstablecimiento == idEstablecimiento
                   && (idAnimal == null || animal.Id == idAnimal)
                   && (!soloActivas || alerta.Estado == EstadoAlerta.Activa)
               select new AlertaRespuesta(
                   alerta.Id, alerta.IdEvento, animal.Id, animal.Identificador, alerta.Tipo, alerta.Prioridad,
                   alerta.Variacion, alerta.GmdEsperada, alerta.Estado, alerta.FechaGeneracion))
            .ToListAsync(cancelacion);

    public async Task<IReadOnlyList<PesajeResumido>> ListarPesajesAsync(
        FiltroPesajes filtro, CancellationToken cancelacion = default)
    {
        var consulta = from evento in contexto.EventosPesaje.AsNoTracking()
                       join animal in contexto.Animales on evento.IdAnimal equals animal.Id
                       where animal.IdEstablecimiento == filtro.IdEstablecimiento
                       select evento;

        if (filtro.IdLote is { } idLote)
        {
            // Use the event's historical assignment, never the animal's current lot.
            consulta = consulta.Where(e => contexto.HistorialLotes.Any(h => h.Id == e.IdHistorialLote && h.IdLote == idLote));
        }

        if (filtro.Desde is { } desde)
        {
            consulta = consulta.Where(e => e.FechaPesaje >= desde);
        }

        if (filtro.Hasta is { } hasta)
        {
            consulta = consulta.Where(e => e.FechaPesaje < hasta);
        }

        return await consulta.OrderBy(e => e.FechaPesaje).ThenBy(e => e.Id)
            .Select(e => new PesajeResumido(e.IdAnimal, e.PesoKg, e.FechaPesaje))
            .ToListAsync(cancelacion);
    }
}
