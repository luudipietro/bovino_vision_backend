namespace BovinoVision.Aplicacion.Capturas;

/// <summary>Lado de lectura del módulo: proyecciones directas desde el almacenamiento, sin seguimiento.</summary>
public interface IConsultasCapturas
{
    Task<CapturaRespuesta?> ObtenerCapturaAsync(Guid idCaptura, CancellationToken cancelacion = default);

    /// <summary>Capturas de la sesión ordenadas por fecha de captura; vacío cuando no tiene ninguna.</summary>
    Task<IReadOnlyList<CapturaDeSesionRespuesta>> ListarDeSesionAsync(Guid idSesion, CancellationToken cancelacion = default);

    /// <summary>Últimas sesiones del usuario, de la más reciente a la más antigua, con cuántas capturas tienen y cuántas están procesadas.</summary>
    Task<IReadOnlyList<SesionRecienteRespuesta>> ListarSesionesRecientesAsync(Guid idUsuario, int limite, CancellationToken cancelacion = default);

    /// <summary>La imagen cuando pertenece a una captura de un animal del establecimiento; null en otro caso.</summary>
    Task<ImagenAlmacenada?> ObtenerImagenAsync(Guid idImagen, Guid idEstablecimiento, CancellationToken cancelacion = default);
}
