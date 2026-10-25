using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Dominio.Capturas;
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

    public async Task<IReadOnlyList<SesionRecienteRespuesta>> ListarSesionesRecientesAsync(
        Guid idUsuario, int limite, CancellationToken cancelacion = default) =>
        await contexto.SesionesCaptura.AsNoTracking()
            .Where(s => s.IdUsuario == idUsuario)
            .OrderByDescending(s => s.FechaInicio).ThenByDescending(s => s.Id)
            .Take(limite)
            .Select(s => new SesionRecienteRespuesta(
                s.Id,
                s.FechaInicio,
                s.FechaFin,
                s.Estado,
                contexto.Capturas.Count(c => c.IdSesion == s.Id),
                contexto.Capturas.Count(c => c.IdSesion == s.Id && c.Estado == EstadoCaptura.Procesada)))
            .ToListAsync(cancelacion);

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
                contexto.EventosPesaje.Where(e => e.IdCaptura == c.Id).Select(e => (Guid?)e.Id).FirstOrDefault(),
                // Una captura puede tener varias imágenes; la primera por id (UUID v7 ordenado por tiempo) mantiene la fila única y estable.
                contexto.Imagenes.Where(i => i.IdCaptura == c.Id).OrderBy(i => i.Id).Select(i => (Guid?)i.Id).FirstOrDefault()))
            .ToListAsync(cancelacion);

    public Task<ImagenAlmacenada?> ObtenerImagenAsync(Guid idImagen, Guid idEstablecimiento, CancellationToken cancelacion = default) =>
        (
            from i in contexto.Imagenes.AsNoTracking()
            join c in contexto.Capturas on i.IdCaptura equals c.Id
            join a in contexto.Animales on c.IdAnimal equals a.Id
            where i.Id == idImagen && a.IdEstablecimiento == idEstablecimiento
            select new ImagenAlmacenada(i.ReferenciaImagen, i.TipoContenido))
            .SingleOrDefaultAsync(cancelacion);
}
