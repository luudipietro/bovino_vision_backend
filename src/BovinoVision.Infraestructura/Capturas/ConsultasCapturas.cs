using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace BovinoVision.Infraestructura.Capturas;

internal sealed class ConsultasCapturas(BovinoVisionDbContext contexto) : IConsultasCapturas
{
    public Task<CapturaRespuesta?> ObtenerCapturaAsync(Guid idCaptura, CancellationToken cancelacion = default) =>
        contexto.Capturas.AsNoTracking()
            .Where(c => c.Id == idCaptura)
            .Select(c => new CapturaRespuesta(
                c.Id,
                c.IdSesion,
                c.IdAnimal,
                c.FechaCaptura,
                c.Estado,
                c.MotivoFallo,
                c.Intentos,
                c.FechaRevision,
                contexto.EventosPesaje.Where(e => e.IdCaptura == c.Id).Select(e => (Guid?)e.Id).FirstOrDefault()))
            .SingleOrDefaultAsync(cancelacion);

    public async Task<IReadOnlyList<CapturaDeSesionRespuesta>> ListarDeSesionAsync(Guid idSesion, CancellationToken cancelacion = default) =>
        await (
            from c in contexto.Capturas.AsNoTracking()
            join a in contexto.Animales on c.IdAnimal equals a.Id
            where c.IdSesion == idSesion
            orderby c.FechaCaptura, c.Id
            select new CapturaDeSesionRespuesta(
                c.Id,
                c.IdAnimal,
                a.Identificador,
                c.FechaCaptura,
                c.Estado,
                c.MotivoFallo,
                contexto.EventosPesaje.Where(e => e.IdCaptura == c.Id).Select(e => (Guid?)e.Id).FirstOrDefault()))
            .ToListAsync(cancelacion);
}
