using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Capturas;

public sealed record ObtenerImagenConsulta(Guid IdImagen);

/// <summary>Where the file of an image lives and the content type it was uploaded with.</summary>
public sealed record ImagenAlmacenada(string Referencia, string TipoContenido);

/// <summary>An image opened for reading; whoever receives it disposes <see cref="Contenido"/>.</summary>
public sealed record ArchivoImagen(Stream Contenido, string TipoContenido);

/// <summary>
/// Serves the photo of a capture. Only images of captures whose animal belongs to the establishment
/// of the session are visible; any other case looks like a missing image.
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
