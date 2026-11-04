using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using BovinoVision.Aplicacion.Capturas;
using Microsoft.Extensions.Options;

namespace BovinoVision.Infraestructura.Capturas;

/// <summary>Bucket S3 compatible (Neon Object Storage) donde se guardan las imágenes cuando se configura uno.</summary>
public sealed class OpcionesAlmacenamientoS3
{
    public const string Seccion = "Almacenamiento:S3";

    public string? Bucket { get; set; }

    /// <summary>Endpoint del servicio, por ejemplo el <c>AWS_ENDPOINT_URL_S3</c> de la rama de Neon.</summary>
    public string? UrlServicio { get; set; }

    public string? Region { get; set; }

    public string? IdClaveAcceso { get; set; }

    public string? ClaveSecreta { get; set; }
}

/// <summary>
/// Guarda las imágenes como objetos de un bucket compartido, así todas las instancias de la API ven las mismas fotos.
/// La referencia es la clave del objeto, con el mismo formato que el almacenamiento local.
/// </summary>
internal sealed class AlmacenamientoS3Imagenes(IAmazonS3 cliente, IOptions<OpcionesAlmacenamientoS3> opciones)
    : IAlmacenamientoImagenes
{
    private readonly string _bucket = opciones.Value.Bucket!;

    public async Task<string> GuardarAsync(string nombreSugerido, ReadOnlyMemory<byte> contenido, CancellationToken cancelacion = default)
    {
        var referencia = ReferenciasDeImagen.Nueva(nombreSugerido);
        using var cuerpo = new MemoryStream(contenido.ToArray(), writable: false);
        // Sin "aws-chunked": el cuerpo viaja entero y firmado, la forma que acepta cualquier servicio S3 compatible.
        await cliente.PutObjectAsync(
            new PutObjectRequest { BucketName = _bucket, Key = referencia, InputStream = cuerpo, UseChunkEncoding = false }, cancelacion);
        return referencia;
    }

    public async Task<Stream?> AbrirAsync(string referencia, CancellationToken cancelacion = default)
    {
        if (!ReferenciasDeImagen.EsNombreDeArchivo(referencia))
        {
            return null;
        }

        try
        {
            // Liberar el stream de la respuesta libera también la conexión HTTP.
            var respuesta = await cliente.GetObjectAsync(_bucket, referencia, cancelacion);
            return respuesta.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task EliminarAsync(string referencia, CancellationToken cancelacion = default)
    {
        if (!ReferenciasDeImagen.EsNombreDeArchivo(referencia))
        {
            throw new ArgumentException($"Referencia de imagen inválida: {referencia}", nameof(referencia));
        }

        // S3 responde bien aunque el objeto no exista, igual que File.Delete con un archivo ausente.
        await cliente.DeleteObjectAsync(_bucket, referencia, cancelacion);
    }
}
