using BovinoVision.Dominio.Usuarios;

namespace BovinoVision.Aplicacion.Usuarios;

public sealed record UsuarioRespuesta(
    Guid Id,
    string Nombre,
    string Correo,
    Guid IdRol,
    string Rol,
    bool Activo,
    DateTimeOffset? BloqueadoHasta,
    DateTimeOffset? UltimoAcceso = null)
{
    public static UsuarioRespuesta Desde(Usuario usuario, Rol rol, DateTimeOffset? ultimoAcceso = null) => new(
        usuario.Id, usuario.Nombre, usuario.Correo, rol.Id, rol.Nombre, usuario.Activo, usuario.BloqueadoHasta, ultimoAcceso);
}

public sealed record SesionRespuesta(UsuarioRespuesta Usuario, IReadOnlyList<Permiso> Permisos, DateTimeOffset ExpiraEn);

/// <summary>El token va solo en la cookie; el cuerpo nunca lo lleva.</summary>
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
