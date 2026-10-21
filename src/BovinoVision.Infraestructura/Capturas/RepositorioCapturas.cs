using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace BovinoVision.Infraestructura.Capturas;

internal sealed class RepositorioCapturas(BovinoVisionDbContext contexto) : IRepositorioCapturas
{
    public Task<Captura?> ObtenerAsync(Guid id, CancellationToken cancelacion = default) =>
        contexto.Capturas.Include(c => c.Imagenes).SingleOrDefaultAsync(c => c.Id == id, cancelacion);

    public void Agregar(Captura captura) => contexto.Capturas.Add(captura);

    public void Eliminar(Captura captura) => contexto.Capturas.Remove(captura);
}
