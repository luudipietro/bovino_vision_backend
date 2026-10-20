using BovinoVision.Dominio.Capturas;

namespace BovinoVision.Aplicacion.Capturas;

public interface IRepositorioCapturas
{
    /// <summary>Loads the capture with its images.</summary>
    Task<Captura?> ObtenerAsync(Guid id, CancellationToken cancelacion = default);

    void Agregar(Captura captura);
}
