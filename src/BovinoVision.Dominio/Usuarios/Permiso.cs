namespace BovinoVision.Dominio.Usuarios;

/// <summary>Functionalities a role can enable (CU-08); stored and serialized as snake_case text.</summary>
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
