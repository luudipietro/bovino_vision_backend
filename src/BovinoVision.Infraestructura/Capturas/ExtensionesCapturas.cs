using BovinoVision.Aplicacion.Capturas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Infraestructura.Capturas;

public static class ExtensionesCapturas
{
    /// <summary>Adaptadores del módulo Capturas: persistencia, consultas de lectura y almacenamiento de imágenes.</summary>
    public static IServiceCollection AgregarInfraestructuraCapturas(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddOptions<OpcionesAlmacenamiento>().Bind(configuracion.GetSection(OpcionesAlmacenamiento.Seccion));

        servicios.AddScoped<IRepositorioSesionesCaptura, RepositorioSesionesCaptura>();
        servicios.AddScoped<IRepositorioCapturas, RepositorioCapturas>();
        servicios.AddScoped<IAnimalesDeCaptura, AnimalesDeCaptura>();
        servicios.AddScoped<IConsultasCapturas, ConsultasCapturas>();
        servicios.AddSingleton<IAlmacenamientoImagenes, AlmacenamientoLocalImagenes>();
        return servicios;
    }
}
