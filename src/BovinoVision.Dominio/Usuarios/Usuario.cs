using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Usuarios;

/// <summary>
/// Minimal user model. Authentication, lockout and role management arrive with the
/// Users module; until then a seeded system user owns every operation.
/// </summary>
public sealed class Usuario : Entidad
{
    public Guid IdEstablecimiento { get; private set; }
    public Guid IdRol { get; private set; }
    public string Nombre { get; private set; } = null!;
    public string Correo { get; private set; } = null!;
    public string ResumenContrasena { get; private set; } = null!;
    public bool Activo { get; private set; }
    public int IntentosFallidos { get; private set; }
    public DateTimeOffset? BloqueadoHasta { get; private set; }

    private Usuario() { }

    public Usuario(Guid id, Guid idEstablecimiento, Guid idRol, string nombre, string correo, string resumenContrasena)
    {
        Id = id;
        IdEstablecimiento = idEstablecimiento;
        IdRol = idRol;
        Nombre = nombre;
        Correo = correo;
        ResumenContrasena = resumenContrasena;
        Activo = true;
    }
}
