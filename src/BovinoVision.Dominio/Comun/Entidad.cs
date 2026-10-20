namespace BovinoVision.Dominio.Comun;

public abstract class Entidad
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
}
