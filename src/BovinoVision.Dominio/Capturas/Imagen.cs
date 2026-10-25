using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Capturas;

/// <summary>Archivo de imagen privado de una captura; la base de datos guarda solo su referencia y su hash.</summary>
public sealed class Imagen : Entidad
{
    public Guid IdCaptura { get; private set; }
    public string ReferenciaImagen { get; private set; } = null!;
    public string HuellaVerificacion { get; private set; } = null!;
    public string TipoContenido { get; private set; } = null!;

    private Imagen() { }

    internal Imagen(Guid idCaptura, string referenciaImagen, string huellaVerificacion, string tipoContenido)
    {
        IdCaptura = idCaptura;
        ReferenciaImagen = referenciaImagen;
        HuellaVerificacion = huellaVerificacion;
        TipoContenido = tipoContenido;
    }
}
