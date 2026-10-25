using System.Security.Claims;
using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Comun;
using Microsoft.AspNetCore.Http;

namespace BovinoVision.Infraestructura.Seguridad;

/// <summary>Usuario y establecimiento de la sesión que autenticó la solicitud actual.</summary>
internal sealed class ContextoUsuarioHttp(IHttpContextAccessor accesor) : IContextoUsuario
{
    public Guid IdUsuario => Leer(ClaimTypes.NameIdentifier);

    public Guid IdEstablecimiento => Leer(SesionHttp.ReclamoEstablecimiento);

    private Guid Leer(string tipo) =>
        Guid.TryParse(accesor.HttpContext?.User.FindFirstValue(tipo), out var valor)
            ? valor
            : throw new NoAutenticadoError("Iniciá sesión para continuar.");
}
