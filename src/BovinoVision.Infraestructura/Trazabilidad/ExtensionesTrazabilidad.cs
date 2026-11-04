using BovinoVision.Aplicacion.Trazabilidad;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Infraestructura.Trazabilidad;

public static class ExtensionesTrazabilidad
{
    public static IServiceCollection AgregarPersistenciaTrazabilidad(this IServiceCollection servicios)
    {
        servicios.AddScoped<IRepositorioLotes, RepositorioLotes>();
        servicios.AddScoped<IRepositorioAnimales, RepositorioAnimales>();
        servicios.AddScoped<ICatalogoRazas, CatalogoRazas>();
        servicios.AddScoped<IConsultasTrazabilidad, ConsultasTrazabilidad>();
        return servicios;
    }
}
