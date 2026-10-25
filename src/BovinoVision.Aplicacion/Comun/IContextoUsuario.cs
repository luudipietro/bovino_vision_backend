namespace BovinoVision.Aplicacion.Comun;

/// <summary>Identidad de quien ejecuta la operación actual: el usuario de la sesión de la solicitud.</summary>
public interface IContextoUsuario
{
    Guid IdUsuario { get; }

    Guid IdEstablecimiento { get; }
}
