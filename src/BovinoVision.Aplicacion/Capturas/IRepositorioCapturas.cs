using BovinoVision.Dominio.Capturas;

namespace BovinoVision.Aplicacion.Capturas;

public interface IRepositorioCapturas
{
    /// <summary>Carga la captura con sus imágenes.</summary>
    Task<Captura?> ObtenerAsync(Guid id, CancellationToken cancelacion = default);

    void Agregar(Captura captura);

    /// <summary>Prepara la eliminación de la captura y sus imágenes.</summary>
    void Eliminar(Captura captura);
}
