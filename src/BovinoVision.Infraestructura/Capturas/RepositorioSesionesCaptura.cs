using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace BovinoVision.Infraestructura.Capturas;

internal sealed class RepositorioSesionesCaptura(BovinoVisionDbContext contexto) : IRepositorioSesionesCaptura
{
    public Task<SesionCaptura?> ObtenerAsync(Guid id, CancellationToken cancelacion = default) =>
        contexto.SesionesCaptura.SingleOrDefaultAsync(s => s.Id == id, cancelacion);

    public void Agregar(SesionCaptura sesion) => contexto.SesionesCaptura.Add(sesion);

    // La fecha de captura la informa el cliente y puede ser anterior al inicio, así que la actividad nunca es menor.
    public async Task<IReadOnlyList<SesionInactiva>> ListarInactivasAsync(
        DateTimeOffset sinActividadDesde, CancellationToken cancelacion = default)
    {
        var filas = await contexto.SesionesCaptura
            .Where(s => s.Estado == EstadoSesionCaptura.Activa)
            .Select(s => new
            {
                Sesion = s,
                UltimaCaptura = contexto.Capturas.Where(c => c.IdSesion == s.Id).Max(c => (DateTimeOffset?)c.FechaCaptura),
            })
            .Select(f => new
            {
                f.Sesion,
                UltimaActividad = f.UltimaCaptura != null && f.UltimaCaptura > f.Sesion.FechaInicio
                    ? f.UltimaCaptura.Value
                    : f.Sesion.FechaInicio,
            })
            .Where(f => f.UltimaActividad < sinActividadDesde)
            .ToListAsync(cancelacion);
        return filas.Select(f => new SesionInactiva(f.Sesion, f.UltimaActividad)).ToList();
    }
}
