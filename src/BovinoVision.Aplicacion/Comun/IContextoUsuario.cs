namespace BovinoVision.Aplicacion.Comun;

/// <summary>
/// Identity of whoever runs the current operation. Until the Users module exists the
/// implementation returns the seeded system user and its establecimiento.
/// </summary>
public interface IContextoUsuario
{
    Guid IdUsuario { get; }

    Guid IdEstablecimiento { get; }
}
