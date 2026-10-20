using BovinoVision.Aplicacion.Analitica;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Infraestructura.Analitica;

public static class ExtensionesAnalitica
{
    /// <summary>EF Core adapters of the Analítica module's ports.</summary>
    public static IServiceCollection AgregarPersistenciaAnalitica(this IServiceCollection servicios)
    {
        servicios.AddScoped<IRepositorioAlertas, RepositorioAlertas>();
        servicios.AddScoped<IConsultasAnalitica, ConsultasAnalitica>();
        return servicios;
    }
}
