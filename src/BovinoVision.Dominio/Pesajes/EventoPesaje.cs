using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Pesajes;

/// <summary>Registro de pesaje inmutable, creado solo a partir de una estimación exitosa.</summary>
public sealed class EventoPesaje : Entidad
{
    public Guid IdAnimal { get; private set; }
    public Guid IdCaptura { get; private set; }
    public Guid IdImagenUtilizada { get; private set; }

    /// <summary>Asignación de lote vigente en FechaPesaje; null cuando el animal no tenía lote en ese momento.</summary>
    public Guid? IdHistorialLote { get; private set; }

    public DateTimeOffset FechaPesaje { get; private set; }
    public decimal PesoKg { get; private set; }
    public DateTimeOffset FechaProcesamiento { get; private set; }
    public string VersionModelo { get; private set; } = null!;

    private EventoPesaje() { }

    public static EventoPesaje Registrar(
        Captura captura,
        Guid idImagenUtilizada,
        Guid? idHistorialLote,
        decimal pesoKg,
        DateTimeOffset fechaProcesamiento,
        string versionModelo)
    {
        if (pesoKg <= 0)
        {
            throw new OperacionInvalidaError("El peso estimado debe ser positivo");
        }

        if (captura.Imagenes.All(i => i.Id != idImagenUtilizada))
        {
            throw new OperacionInvalidaError($"La imagen {idImagenUtilizada} no pertenece a la captura {captura.Id}");
        }

        return new EventoPesaje
        {
            IdAnimal = captura.IdAnimal,
            IdCaptura = captura.Id,
            IdImagenUtilizada = idImagenUtilizada,
            IdHistorialLote = idHistorialLote,
            FechaPesaje = captura.FechaCaptura,
            PesoKg = pesoKg,
            FechaProcesamiento = fechaProcesamiento.ToUniversalTime(),
            VersionModelo = versionModelo,
        };
    }
}
