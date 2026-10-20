using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Capturas;

/// <summary>Private image file of a capture; the database keeps only its reference and hash.</summary>
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
