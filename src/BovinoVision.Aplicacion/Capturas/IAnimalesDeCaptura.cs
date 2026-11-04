namespace BovinoVision.Aplicacion.Capturas;

/// <summary>Lo que el módulo Capturas necesita saber de los animales.</summary>
public interface IAnimalesDeCaptura
{
    Task<bool> ExisteAsync(Guid idAnimal, Guid idEstablecimiento, CancellationToken cancelacion = default);
}
