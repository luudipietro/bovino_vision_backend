using System.Reflection;

namespace BovinoVision.Api.Modulos;

/// <summary>
/// A functional module of the system (Trazabilidad, Capturas, Analítica, ...). Each module
/// registers its own services and endpoints, so adding one never touches Program.cs.
/// </summary>
public interface IModulo
{
    void AgregarServicios(IServiceCollection servicios, IConfiguration configuracion);

    void MapearEndpoints(IEndpointRouteBuilder rutas);
}

public static class ExtensionesDeModulos
{
    private static readonly IReadOnlyList<IModulo> Modulos = Descubrir(typeof(IModulo).Assembly);

    public static IServiceCollection AgregarModulos(this IServiceCollection servicios, IConfiguration configuracion)
    {
        foreach (var modulo in Modulos)
        {
            modulo.AgregarServicios(servicios, configuracion);
        }

        return servicios;
    }

    public static IEndpointRouteBuilder MapearModulos(this IEndpointRouteBuilder rutas)
    {
        foreach (var modulo in Modulos)
        {
            modulo.MapearEndpoints(rutas);
        }

        return rutas;
    }

    private static List<IModulo> Descubrir(Assembly ensamblado) =>
        ensamblado.GetTypes()
            .Where(tipo => tipo is { IsClass: true, IsAbstract: false } && typeof(IModulo).IsAssignableFrom(tipo))
            .OrderBy(tipo => tipo.FullName, StringComparer.Ordinal)
            .Select(tipo => (IModulo)Activator.CreateInstance(tipo)!)
            .ToList();
}
