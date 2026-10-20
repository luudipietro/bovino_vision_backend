using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Usuarios;

public sealed class Rol : Entidad
{
    public Guid IdEstablecimiento { get; private set; }
    public string Nombre { get; private set; } = null!;

    private Rol() { }

    public Rol(Guid id, Guid idEstablecimiento, string nombre)
    {
        Id = id;
        IdEstablecimiento = idEstablecimiento;
        Nombre = nombre;
    }
}
