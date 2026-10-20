using BovinoVision.Aplicacion.Comun;
using BovinoVision.Infraestructura.Persistencia;

namespace BovinoVision.Infraestructura.Seguridad;

/// <summary>Seeded system user and establecimiento, used until authentication exists.</summary>
internal sealed class ContextoUsuarioPorDefecto : IContextoUsuario
{
    public Guid IdUsuario => DatosSemilla.IdUsuarioSistema;

    public Guid IdEstablecimiento => DatosSemilla.IdEstablecimiento;
}
