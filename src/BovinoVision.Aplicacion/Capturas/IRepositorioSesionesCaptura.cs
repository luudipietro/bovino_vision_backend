using BovinoVision.Dominio.Capturas;

namespace BovinoVision.Aplicacion.Capturas;

public interface IRepositorioSesionesCaptura
{
    Task<SesionCaptura?> ObtenerAsync(Guid id, CancellationToken cancelacion = default);

    void Agregar(SesionCaptura sesion);
}
