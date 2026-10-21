using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Usuarios;

public sealed class Rol : Entidad
{
    public Guid IdEstablecimiento { get; private set; }
    public string Nombre { get; private set; } = null!;
    public IReadOnlyList<Permiso> Permisos { get; private set; } = [];

    /// <summary>An administrator is whoever can manage roles; the system must always keep one.</summary>
    public bool EsAdministrador => Tiene(Permiso.RolesGestionar);

    private Rol() { }

    public Rol(Guid idEstablecimiento, string nombre, IEnumerable<Permiso> permisos)
    {
        IdEstablecimiento = idEstablecimiento;
        Nombre = nombre;
        DefinirPermisos(permisos);
    }

    public bool Tiene(Permiso permiso) => Permisos.Contains(permiso);

    public void DefinirPermisos(IEnumerable<Permiso> permisos) => Permisos = [.. permisos.Distinct().Order()];
}
