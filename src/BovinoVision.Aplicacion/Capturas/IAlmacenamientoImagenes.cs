namespace BovinoVision.Aplicacion.Capturas;

/// <summary>Keeps the private image files of captures outside the database.</summary>
public interface IAlmacenamientoImagenes
{
    /// <summary>Stores the content and returns the reference that identifies it from now on.</summary>
    Task<string> GuardarAsync(string nombreSugerido, ReadOnlyMemory<byte> contenido, CancellationToken cancelacion = default);

    /// <summary>
    /// Opens the stored content for reading; the caller disposes the stream. Returns null when the file no
    /// longer exists or the reference does not point inside the storage.
    /// </summary>
    Task<Stream?> AbrirAsync(string referencia, CancellationToken cancelacion = default);

    Task EliminarAsync(string referencia, CancellationToken cancelacion = default);
}
