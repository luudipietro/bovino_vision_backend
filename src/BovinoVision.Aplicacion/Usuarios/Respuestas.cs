using BovinoVision.Dominio.Usuarios;

namespace BovinoVision.Aplicacion.Usuarios;

public sealed record UsuarioRespuesta(
    Guid Id,
    string Nombre,
    string Correo,
    Guid IdRol,
    string Rol,
    bool Activo,
    DateTimeOffset? BloqueadoHasta)
{
    public static UsuarioRespuesta Desde(Usuario usuario, Rol rol) => new(
        usuario.Id, usuario.Nombre, usuario.Correo, rol.Id, rol.Nombre, usuario.Activo, usuario.BloqueadoHasta);
}

public sealed record SesionRespuesta(UsuarioRespuesta Usuario, IReadOnlyList<Permiso> Permisos, DateTimeOffset ExpiraEn);

/// <summary>The token goes only into the cookie; the body never carries it.</summary>
public sealed record SesionIniciada(string Token, SesionRespuesta Sesion);

public sealed record SesionActiva(
    Guid IdSesion,
    Guid IdEstablecimiento,
    DateTimeOffset ExpiraEn,
    DateTimeOffset? RevocadaEn,
    UsuarioRespuesta Usuario,
    IReadOnlyList<Permiso> Permisos)
{
    public bool EsValidaEn(DateTimeOffset ahora) => RevocadaEn is null && ahora < ExpiraEn && Usuario.Activo;

    public SesionRespuesta ARespuesta() => new(Usuario, Permisos, ExpiraEn);
}

public sealed record RolRespuesta(Guid Id, string Nombre, IReadOnlyList<Permiso> Permisos, int CantidadUsuarios);

public sealed record PermisoRespuesta(Permiso Codigo, string Descripcion);
