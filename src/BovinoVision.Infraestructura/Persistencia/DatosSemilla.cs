using BovinoVision.Dominio.Usuarios;

namespace BovinoVision.Infraestructura.Persistencia;

/// <summary>
/// Fixed identifiers of the seeded rows. The single establecimiento and the system user
/// stand in for real tenants and users until the Users module exists.
/// </summary>
public static class DatosSemilla
{
    public static readonly Guid IdEstablecimiento = new("01920000-0000-7000-8000-000000000001");
    public static readonly Guid IdRolAdministrador = new("01920000-0000-7000-8000-000000000002");
    public static readonly Guid IdUsuarioSistema = new("01920000-0000-7000-8000-000000000003");
    public static readonly Guid IdRolProductor = new("01920000-0000-7000-8000-000000000004");
    public static readonly Guid IdRolOperario = new("01920000-0000-7000-8000-000000000005");

    /// <summary>Initial roles, following the actors of each use case in the ERS (editable through CU-08).</summary>
    public static readonly IReadOnlyList<(Guid Id, string Nombre, Permiso[] Permisos)> Roles =
    [
        (IdRolAdministrador, "Administrador", [Permiso.UsuariosGestionar, Permiso.RolesGestionar, Permiso.AuditoriaConsultar]),
        (IdRolProductor, "Productor Ganadero", [Permiso.RodeoGestionar, Permiso.RodeoConsultar, Permiso.AnaliticaConsultar]),
        (IdRolOperario, "Operario de Campo", [Permiso.CapturasRegistrar, Permiso.RodeoGestionar, Permiso.RodeoConsultar]),
    ];

    public const string NombreEstablecimiento = "Establecimiento principal";
    public const string CorreoUsuarioSistema = "sistema@bovinovision.local";

    /// <summary>Not a valid hash, so nobody can log in as the system user (which is also inactive).</summary>
    public const string ContrasenaInutilizable = "!";

    public static readonly IReadOnlyList<(Guid Id, string Nombre)> Razas =
    [
        (new Guid("01920000-0000-7000-8000-000000000101"), "Aberdeen Angus"),
        (new Guid("01920000-0000-7000-8000-000000000102"), "Hereford"),
        (new Guid("01920000-0000-7000-8000-000000000103"), "Brangus"),
        (new Guid("01920000-0000-7000-8000-000000000104"), "Braford"),
        (new Guid("01920000-0000-7000-8000-000000000105"), "Shorthorn"),
        (new Guid("01920000-0000-7000-8000-000000000106"), "Holando Argentino"),
        (new Guid("01920000-0000-7000-8000-000000000107"), "Cruza"),
    ];
}
