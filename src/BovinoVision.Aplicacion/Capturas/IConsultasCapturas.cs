namespace BovinoVision.Aplicacion.Capturas;

/// <summary>Read side of the module: projections straight from storage, without tracking.</summary>
public interface IConsultasCapturas
{
    Task<CapturaRespuesta?> ObtenerCapturaAsync(Guid idCaptura, CancellationToken cancelacion = default);

    /// <summary>Captures of the session ordered by capture date; empty when it has none.</summary>
    Task<IReadOnlyList<CapturaDeSesionRespuesta>> ListarDeSesionAsync(Guid idSesion, CancellationToken cancelacion = default);

    /// <summary>The image when it belongs to a capture of an animal of the establishment; null otherwise.</summary>
    Task<ImagenAlmacenada?> ObtenerImagenAsync(Guid idImagen, Guid idEstablecimiento, CancellationToken cancelacion = default);
}
