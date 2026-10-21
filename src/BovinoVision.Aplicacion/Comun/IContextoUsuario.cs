namespace BovinoVision.Aplicacion.Comun;

/// <summary>Identity of whoever runs the current operation: the user of the request's session.</summary>
public interface IContextoUsuario
{
    Guid IdUsuario { get; }

    Guid IdEstablecimiento { get; }
}
