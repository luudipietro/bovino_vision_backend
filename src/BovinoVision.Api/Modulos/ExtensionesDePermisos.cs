using BovinoVision.Dominio.Usuarios;
using BovinoVision.Infraestructura.Seguridad;

namespace BovinoVision.Api.Modulos;

public static class ExtensionesDePermisos
{
    /// <summary>The endpoint answers 403 to sessions whose role lacks the permission.</summary>
    public static TBuilder RequierePermiso<TBuilder>(this TBuilder endpoint, Permiso permiso)
        where TBuilder : IEndpointConventionBuilder =>
        endpoint.RequireAuthorization(SesionHttp.Politica(permiso));
}
