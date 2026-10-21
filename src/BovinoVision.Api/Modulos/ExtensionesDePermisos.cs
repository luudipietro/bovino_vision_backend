using BovinoVision.Dominio.Usuarios;
using BovinoVision.Infraestructura.Seguridad;

namespace BovinoVision.Api.Modulos;

public static class ExtensionesDePermisos
{
    /// <summary>The endpoint answers 403 to sessions whose role lacks the permission.</summary>
    public static TBuilder RequierePermiso<TBuilder>(this TBuilder endpoint, Permiso permiso)
        where TBuilder : IEndpointConventionBuilder =>
        endpoint.RequireAuthorization(SesionHttp.Politica(permiso));

    /// <summary>The endpoint accepts sessions with at least one of the permissions.</summary>
    public static TBuilder RequiereAlgunPermiso<TBuilder>(this TBuilder endpoint, params Permiso[] permisos)
        where TBuilder : IEndpointConventionBuilder =>
        endpoint.RequireAuthorization(politica => politica.RequireAssertion(contexto =>
            permisos.Any(permiso => contexto.User.HasClaim(SesionHttp.ReclamoPermiso, SesionHttp.Politica(permiso)))));
}
