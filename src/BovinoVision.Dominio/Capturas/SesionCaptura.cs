using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Capturas;

public sealed class SesionCaptura : Entidad
{
    public Guid IdUsuario { get; private set; }
    public DateTimeOffset FechaInicio { get; private set; }
    public DateTimeOffset? FechaFin { get; private set; }
    public EstadoSesionCaptura Estado { get; private set; }

    private SesionCaptura() { }

    public SesionCaptura(Guid idUsuario, DateTimeOffset fechaInicio)
    {
        IdUsuario = idUsuario;
        FechaInicio = fechaInicio.ToUniversalTime();
        Estado = EstadoSesionCaptura.Activa;
    }

    public void Finalizar(DateTimeOffset fecha)
    {
        if (Estado == EstadoSesionCaptura.Finalizada)
        {
            throw new OperacionInvalidaError("La sesión de captura ya está finalizada");
        }

        FechaFin = fecha.ToUniversalTime();
        Estado = EstadoSesionCaptura.Finalizada;
    }
}

public enum EstadoSesionCaptura
{
    Activa,
    Finalizada,
}
