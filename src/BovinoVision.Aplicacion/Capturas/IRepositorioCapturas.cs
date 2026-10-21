using BovinoVision.Dominio.Capturas;

namespace BovinoVision.Aplicacion.Capturas;

public interface IRepositorioCapturas
{
    /// <summary>Loads the capture with its images.</summary>
    Task<Captura?> ObtenerAsync(Guid id, CancellationToken cancelacion = default);

    void Agregar(Captura captura);

    /// <summary>Prepares the removal of the capture and its images.</summary>
    void Eliminar(Captura captura);
}
