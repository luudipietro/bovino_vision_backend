using BovinoVision.Aplicacion.Pesajes;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace BovinoVision.Infraestructura.Pesajes;

internal sealed class ConsultasPesajes(BovinoVisionDbContext contexto) : IConsultasPesajes
{
    public async Task<PesajeRespuesta?> ObtenerAsync(Guid id, CancellationToken cancelacion = default)
    {
        var evento = await contexto.EventosPesaje.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, cancelacion);
        if (evento is null) { return null; }
        var identificador = await contexto.Animales.Where(a => a.Id == evento.IdAnimal).Select(a => a.Identificador).SingleAsync(cancelacion);
        var lote = await (from h in contexto.HistorialLotes
                          join l in contexto.Lotes on h.IdLote equals l.Id
                          where h.Id == evento.IdHistorialLote
                          select l.Codigo).SingleOrDefaultAsync(cancelacion);
        var previo = await contexto.EventosPesaje.AsNoTracking()
            .Where(e => e.IdAnimal == evento.IdAnimal && (e.FechaPesaje < evento.FechaPesaje ||
                (e.FechaPesaje == evento.FechaPesaje && e.Id.CompareTo(evento.Id) < 0)))
            .OrderByDescending(e => e.FechaPesaje).ThenByDescending(e => e.Id)
            .Select(e => new PesoPrevioRespuesta(e.PesoKg, e.FechaPesaje)).FirstOrDefaultAsync(cancelacion);
        var alerta = await contexto.Alertas.AsNoTracking().SingleOrDefaultAsync(a => a.IdEvento == id, cancelacion);
        return new(evento.Id, evento.IdAnimal, evento.IdCaptura, "estimado", evento.PesoKg, evento.FechaPesaje,
            evento.VersionModelo, identificador, lote, previo, alerta);
    }
}
