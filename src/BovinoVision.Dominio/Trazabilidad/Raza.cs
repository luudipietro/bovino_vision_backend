using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Trazabilidad;

public sealed class Raza : Entidad
{
    public string Nombre { get; private set; } = null!;

    private Raza() { }

    public Raza(Guid id, string nombre)
    {
        Id = id;
        Nombre = nombre;
    }
}
