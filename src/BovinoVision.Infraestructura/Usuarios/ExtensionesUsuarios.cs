using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Dominio.Usuarios;
using BovinoVision.Infraestructura.Persistencia;
using BovinoVision.Infraestructura.Seguridad;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BovinoVision.Infraestructura.Usuarios;

public static partial class ExtensionesUsuarios
{
    private const string SeccionAutenticacion = "Autenticacion";

    /// <summary>Adaptadores del módulo Usuarios más la autenticación por sesión y las políticas de permisos.</summary>
    public static IServiceCollection AgregarInfraestructuraUsuarios(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var horas = configuracion.GetValue<double?>($"{SeccionAutenticacion}:DuracionSesionHoras") ?? 12;
        if (horas <= 0)
        {
            throw new InvalidOperationException("Autenticacion:DuracionSesionHoras debe ser positiva");
        }

        servicios.AddSingleton(new OpcionesSesion { Duracion = TimeSpan.FromHours(horas) });
        servicios.AddScoped<IRepositorioUsuarios, RepositorioUsuarios>();
        servicios.AddScoped<IRepositorioRoles, RepositorioRoles>();
        servicios.AddScoped<IRepositorioSesionesUsuario, RepositorioSesionesUsuario>();
        servicios.AddScoped<IConsultasSesion, ConsultasSesion>();
        servicios.AddSingleton<IHasherContrasenas, HasherContrasenas>();

        servicios.AddAuthentication(SesionHttp.Esquema)
            .AddScheme<AuthenticationSchemeOptions, ManejadorAutenticacionSesion>(SesionHttp.Esquema, _ => { });
        servicios.AddAuthorization(opciones =>
        {
            // Todo endpoint exige una sesión salvo que permita explícitamente el acceso anónimo.
            opciones.FallbackPolicy = opciones.DefaultPolicy;
            foreach (var permiso in Enum.GetValues<Permiso>())
            {
                opciones.AddPolicy(SesionHttp.Politica(permiso), politica =>
                    politica.RequireClaim(SesionHttp.ReclamoPermiso, SesionHttp.Politica(permiso)));
            }
        });
        return servicios;
    }

    /// <summary>Crea el primer administrador configurado (D8); las credenciales nunca viven en el repositorio.</summary>
    public static async Task CrearAdministradorInicialAsync(
        this IServiceProvider proveedor, IConfiguration configuracion, CancellationToken cancelacion = default)
    {
        var seccion = configuracion.GetSection($"{SeccionAutenticacion}:AdministradorInicial");
        var correo = seccion["Correo"];
        if (string.IsNullOrWhiteSpace(correo))
        {
            return;
        }

        await using var alcance = proveedor.CreateAsyncScope();
        var caso = alcance.ServiceProvider.GetRequiredService<CrearAdministradorInicial>();
        var creado = await caso.EjecutarAsync(new AdministradorInicial(
            DatosSemilla.IdEstablecimiento,
            DatosSemilla.IdRolAdministrador,
            seccion["Nombre"] ?? "Administrador",
            correo,
            seccion["Contrasena"] ?? string.Empty), cancelacion);
        if (creado)
        {
            var registro = alcance.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ExtensionesUsuarios));
            var correoNormalizado = Usuario.NormalizarCorreo(correo);
            AdministradorCreado(registro, correoNormalizado);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Se creó el administrador inicial {Correo}")]
    private static partial void AdministradorCreado(ILogger registro, string correo);
}
