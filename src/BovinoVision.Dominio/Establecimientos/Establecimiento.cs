using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Establecimientos;

public sealed class Establecimiento : Entidad
{
    public string Nombre { get; private set; } = null!;
    public string? Ubicacion { get; private set; }

    private Establecimiento() { }

    public Establecimiento(Guid id, string nombre, string? ubicacion = null)
    {
        Id = id;
        Nombre = nombre;
        Ubicacion = ubicacion;
    }
}
