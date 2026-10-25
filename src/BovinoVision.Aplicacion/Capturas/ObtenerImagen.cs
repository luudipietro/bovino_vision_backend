using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Capturas;

public sealed record ObtenerImagenConsulta(Guid IdImagen);

/// <summary>Dónde vive el archivo de una imagen y el tipo de contenido con el que se subió.</summary>
public sealed record ImagenAlmacenada(string Referencia, string TipoContenido);

/// <summary>Una imagen abierta para lectura; quien la recibe libera <see cref="Contenido"/>.</summary>
public sealed record ArchivoImagen(Stream Contenido, string TipoContenido);

/// <summary>
/// Sirve la foto de una captura. Solo son visibles las imágenes de capturas cuyo animal pertenece al establecimiento
/// de la sesión; cualquier otro caso se ve como una imagen inexistente.
/// </summary>
public sealed class ObtenerImagen(IConsultasCapturas consultas, IAlmacenamientoImagenes almacenamiento, IContextoUsuario usuario)
{
    public async Task<ArchivoImagen> EjecutarAsync(ObtenerImagenConsulta consulta, CancellationToken cancelacion = default)
    {
        var imagen = await consultas.ObtenerImagenAsync(consulta.IdImagen, usuario.IdEstablecimiento, cancelacion)
            ?? throw NoEncontradoError.De("la imagen", consulta.IdImagen);
        var contenido = await almacenamiento.AbrirAsync(imagen.Referencia, cancelacion)
            ?? throw NoEncontradoError.De("la imagen", consulta.IdImagen);
        return new ArchivoImagen(contenido, imagen.TipoContenido);
    }
}
