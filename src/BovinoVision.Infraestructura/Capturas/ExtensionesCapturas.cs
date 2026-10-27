using Amazon.Runtime;
using Amazon.S3;
using BovinoVision.Aplicacion.Capturas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BovinoVision.Infraestructura.Capturas;

public static class ExtensionesCapturas
{
    /// <summary>Adaptadores del módulo Capturas: persistencia, consultas de lectura y almacenamiento de imágenes.</summary>
    public static IServiceCollection AgregarInfraestructuraCapturas(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddScoped<IRepositorioSesionesCaptura, RepositorioSesionesCaptura>();
        servicios.AddScoped<IRepositorioCapturas, RepositorioCapturas>();
        servicios.AddScoped<IAnimalesDeCaptura, AnimalesDeCaptura>();
        servicios.AddScoped<IConsultasCapturas, ConsultasCapturas>();
        servicios.AgregarAlmacenamiento(configuracion);
        return servicios;
    }

    // Sin bucket configurado las imágenes van al disco local (desarrollo, tests y Compose).
    private static void AgregarAlmacenamiento(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var seccionS3 = configuracion.GetSection(OpcionesAlmacenamientoS3.Seccion);
        if (string.IsNullOrWhiteSpace(seccionS3[nameof(OpcionesAlmacenamientoS3.Bucket)]))
        {
            servicios.AddOptions<OpcionesAlmacenamiento>().Bind(configuracion.GetSection(OpcionesAlmacenamiento.Seccion));
            servicios.AddSingleton<IAlmacenamientoImagenes, AlmacenamientoLocalImagenes>();
            return;
        }

        servicios.AddOptions<OpcionesAlmacenamientoS3>().Bind(seccionS3)
            .Validate(o => Uri.TryCreate(o.UrlServicio, UriKind.Absolute, out var url) && url.Scheme is "http" or "https"
                    && !string.IsNullOrWhiteSpace(o.Region) && !string.IsNullOrWhiteSpace(o.IdClaveAcceso)
                    && !string.IsNullOrWhiteSpace(o.ClaveSecreta),
                "Almacenamiento:S3 necesita una UrlServicio http(s) absoluta, una Region, un IdClaveAcceso y una ClaveSecreta")
            .ValidateOnStart();
        servicios.AddSingleton(proveedor =>
        {
            var opciones = proveedor.GetRequiredService<IOptions<OpcionesAlmacenamientoS3>>().Value;
            return new AmazonS3Config
            {
                ServiceURL = opciones.UrlServicio,
                AuthenticationRegion = opciones.Region,
                // Neon Object Storage solo admite direccionamiento por ruta (endpoint/bucket/clave).
                ForcePathStyle = true,
                // Los checksums CRC por defecto del SDK v4 no son parte del contrato S3 que exige un servicio compatible.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            };
        });
        servicios.AddSingleton<IAmazonS3>(proveedor =>
        {
            var opciones = proveedor.GetRequiredService<IOptions<OpcionesAlmacenamientoS3>>().Value;
            return new AmazonS3Client(
                new BasicAWSCredentials(opciones.IdClaveAcceso, opciones.ClaveSecreta), proveedor.GetRequiredService<AmazonS3Config>());
        });
        servicios.AddSingleton<IAlmacenamientoImagenes, AlmacenamientoS3Imagenes>();
    }
}
