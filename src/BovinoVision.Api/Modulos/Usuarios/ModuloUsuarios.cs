using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Infraestructura.Seguridad;
using BovinoVision.Infraestructura.Usuarios;

namespace BovinoVision.Api.Modulos.Usuarios;

/// <summary>Usuarios module: login sessions (CU-01, CU-02), users, roles and permissions (CU-03 to CU-08).</summary>
public sealed class ModuloUsuarios : IModulo
{
    public void AgregarServicios(IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddScoped<IniciarSesion>();
        servicios.AddScoped<CerrarSesion>();
        servicios.AddScoped<ValidarSesion>();
        servicios.AddScoped<CrearAdministradorInicial>();
        servicios.AgregarInfraestructuraUsuarios(configuracion);
    }

    public void MapearEndpoints(IEndpointRouteBuilder rutas)
    {
        var sesion = rutas.MapGroup("/auth/sesion").WithTags("autenticacion");
        sesion.MapPost("", IniciarAsync).AllowAnonymous();
        sesion.MapGet("", Obtener);
        sesion.MapDelete("", CerrarAsync);
    }

    private static async Task<IResult> IniciarAsync(
        CredencialesUsuario credenciales, IniciarSesion casoDeUso, HttpContext contexto, CancellationToken cancelacion)
    {
        var iniciada = await casoDeUso.EjecutarAsync(credenciales, cancelacion);
        contexto.Response.Cookies.Append(SesionHttp.Cookie, iniciada.Token, SesionHttp.OpcionesCookie(iniciada.Sesion.ExpiraEn));
        return Results.Ok(iniciada.Sesion);
    }

    private static IResult Obtener(HttpContext contexto) =>
        contexto.SesionActual() is { } sesion ? Results.Ok(sesion.ARespuesta()) : Results.Unauthorized();

    private static async Task<IResult> CerrarAsync(HttpContext contexto, CerrarSesion casoDeUso, CancellationToken cancelacion)
    {
        if (contexto.Request.Cookies.TryGetValue(SesionHttp.Cookie, out var token) && !string.IsNullOrEmpty(token))
        {
            await casoDeUso.EjecutarAsync(token, cancelacion);
        }

        contexto.Response.Cookies.Delete(SesionHttp.Cookie, SesionHttp.OpcionesCookie(DateTimeOffset.UnixEpoch));
        return Results.NoContent();
    }
}
