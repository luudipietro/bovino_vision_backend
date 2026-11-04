namespace BovinoVision.Dominio.Usuarios;

/// <summary>Funcionalidades que un rol puede habilitar (CU-08); se guardan y serializan como texto snake_case.</summary>
public enum Permiso
{
    CapturasRegistrar,
    RodeoGestionar,
    RodeoConsultar,
    AnaliticaConsultar,
    UsuariosGestionar,
    RolesGestionar,
    AuditoriaConsultar,
}
