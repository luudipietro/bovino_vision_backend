using BovinoVision.Dominio.Usuarios;
using BovinoVision.Infraestructura.Seguridad;

namespace BovinoVision.Api.Modulos;

public static class ExtensionesDePermisos
{
    /// <summary>El endpoint responde 403 a las sesiones cuyo rol no tiene el permiso.</summary>
    public static TBuilder RequierePermiso<TBuilder>(this TBuilder endpoint, Permiso permiso)
        where TBuilder : IEndpointConventionBuilder =>
        endpoint.RequireAuthorization(SesionHttp.Politica(permiso));

    /// <summary>El endpoint acepta sesiones con al menos uno de los permisos.</summary>
    public static TBuilder RequiereAlgunPermiso<TBuilder>(this TBuilder endpoint, params Permiso[] permisos)
        where TBuilder : IEndpointConventionBuilder =>
        endpoint.RequireAuthorization(politica => politica.RequireAssertion(contexto =>
            permisos.Any(permiso => contexto.User.HasClaim(SesionHttp.ReclamoPermiso, SesionHttp.Politica(permiso)))));
}
