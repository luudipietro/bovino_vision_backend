namespace BovinoVision.Aplicacion.Capturas;

/// <summary>Guarda los archivos de imagen privados de las capturas fuera de la base de datos.</summary>
public interface IAlmacenamientoImagenes
{
    /// <summary>Almacena el contenido y devuelve la referencia que lo identifica de ahora en más.</summary>
    Task<string> GuardarAsync(string nombreSugerido, ReadOnlyMemory<byte> contenido, CancellationToken cancelacion = default);

    /// <summary>
    /// Abre el contenido almacenado para lectura; el llamador libera el stream. Devuelve null cuando el archivo ya
    /// no existe o la referencia no apunta dentro del almacenamiento.
    /// </summary>
    Task<Stream?> AbrirAsync(string referencia, CancellationToken cancelacion = default);

    Task EliminarAsync(string referencia, CancellationToken cancelacion = default);
}
