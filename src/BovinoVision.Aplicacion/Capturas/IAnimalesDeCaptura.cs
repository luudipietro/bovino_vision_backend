namespace BovinoVision.Aplicacion.Capturas;

/// <summary>What the Capturas module needs to know about animals.</summary>
public interface IAnimalesDeCaptura
{
    Task<bool> ExisteAsync(Guid idAnimal, Guid idEstablecimiento, CancellationToken cancelacion = default);
}
