using System.Security.Claims;
using System.Text.Encodings.Web;
using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Dominio.Usuarios;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BovinoVision.Infraestructura.Seguridad;

/// <summary>Names shared by the authentication handler, the user context and the endpoints.</summary>
public static class SesionHttp
{
    public const string Esquema = "SesionBovinoVision";
    public const string Cookie = "bovinovision_sesion";
    public const string ReclamoPermiso = "permiso";
    public const string ReclamoEstablecimiento = "id_establecimiento";

    /// <summary>Authorization policy that requires one permission.</summary>
    public static string Politica(Permiso permiso) => $"permiso:{ConvencionesDeNombres.ASnakeCase(permiso.ToString())}";

    public static CookieOptions OpcionesCookie(DateTimeOffset expira) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expira,
    };

    /// <summary>The session validated for this request, set by the handler.</summary>
    public static SesionActiva? SesionActual(this HttpContext contexto) =>
        contexto.Items.TryGetValue(typeof(SesionActiva), out var sesion) ? sesion as SesionActiva : null;
}

/// <summary>Validates the session cookie on every request and turns the session into claims.</summary>
internal sealed class ManejadorAutenticacionSesion(
    IOptionsMonitor<AuthenticationSchemeOptions> opciones,
    ILoggerFactory registro,
    UrlEncoder codificador)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, registro, codificador)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(SesionHttp.Cookie, out var token) || string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        var validar = Context.RequestServices.GetRequiredService<ValidarSesion>();
        var sesion = await validar.EjecutarAsync(token, Context.RequestAborted);
        if (sesion is null)
        {
            return AuthenticateResult.Fail("Sesión inexistente, vencida o revocada");
        }

        Context.Items[typeof(SesionActiva)] = sesion;
        List<Claim> reclamos =
        [
            new(ClaimTypes.NameIdentifier, sesion.Usuario.Id.ToString()),
            new(ClaimTypes.Name, sesion.Usuario.Nombre),
            new(SesionHttp.ReclamoEstablecimiento, sesion.IdEstablecimiento.ToString()),
            .. sesion.Permisos.Select(p => new Claim(SesionHttp.ReclamoPermiso, SesionHttp.Politica(p))),
        ];
        var identidad = new ClaimsIdentity(reclamos, SesionHttp.Esquema);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad), SesionHttp.Esquema));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        EscribirProblema(StatusCodes.Status401Unauthorized, "Iniciá sesión para continuar.");

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        EscribirProblema(StatusCodes.Status403Forbidden, "No tenés permiso para esta operación.");

    private async Task EscribirProblema(int estado, string detalle)
    {
        Response.StatusCode = estado;
        var problemas = Context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemas.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails = new ProblemDetails { Status = estado, Detail = detalle },
        });
    }
}
