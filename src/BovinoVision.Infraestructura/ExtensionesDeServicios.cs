using BovinoVision.Aplicacion.Analitica;
using BovinoVision.Aplicacion.Comun;
using BovinoVision.Infraestructura.Persistencia;
using BovinoVision.Infraestructura.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BovinoVision.Infraestructura;

public static class ExtensionesDeServicios
{
    public const string NombreConexion = "BovinoVision";

    /// <summary>Infraestructura compartida de la que dependen todos los módulos: persistencia, unidad de trabajo y contexto de usuario.</summary>
    public static IServiceCollection AgregarInfraestructuraBase(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var conexion = configuracion.GetConnectionString(NombreConexion)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión '{NombreConexion}'");

        servicios.AddDbContext<BovinoVisionDbContext>(opciones => opciones.UseNpgsql(conexion));
        servicios.AddScoped<IUnidadDeTrabajo>(proveedor => proveedor.GetRequiredService<BovinoVisionDbContext>());
        servicios.AddHttpContextAccessor();
        servicios.AddSingleton<IContextoUsuario, ContextoUsuarioHttp>();
        servicios.TryAddSingleton(TimeProvider.System);

        // Lo reemplaza el evaluador del módulo Analítica cuando se registra.
        servicios.TryAddScoped<IEvaluadorAlertas, EvaluadorAlertasNulo>();
        return servicios;
    }

    public static async Task AplicarMigracionesAsync(this IServiceProvider proveedor, CancellationToken cancelacion = default)
    {
        await using var alcance = proveedor.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
        await contexto.Database.MigrateAsync(cancelacion);
    }
}
