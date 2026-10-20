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
}
