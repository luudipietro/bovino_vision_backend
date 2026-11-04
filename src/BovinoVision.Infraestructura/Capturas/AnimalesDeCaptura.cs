using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace BovinoVision.Infraestructura.Capturas;

internal sealed class AnimalesDeCaptura(BovinoVisionDbContext contexto) : IAnimalesDeCaptura
{
    public Task<bool> ExisteAsync(Guid idAnimal, Guid idEstablecimiento, CancellationToken cancelacion = default) =>
        contexto.Animales.AnyAsync(a => a.Id == idAnimal && a.IdEstablecimiento == idEstablecimiento, cancelacion);
}
