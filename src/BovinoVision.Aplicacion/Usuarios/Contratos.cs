using BovinoVision.Dominio.Usuarios;

namespace BovinoVision.Aplicacion.Usuarios;

public interface IRepositorioUsuarios
{
    /// <summary>El correo ya debe estar normalizado con <see cref="Usuario.NormalizarCorreo"/>.</summary>
    Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken cancelacion = default);

    Task<Usuario?> ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion = default);

    Task<bool> ExisteCorreoAsync(string correo, Guid? exceptoId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<Usuario>> ListarAsync(Guid idEstablecimiento, Guid? idRol, CancellationToken cancelacion = default);

    /// <summary>Usuarios activos cuyo rol puede gestionar roles, dejando afuera a un usuario o a un rol.</summary>
    Task<int> ContarAdministradoresActivosAsync(
        Guid idEstablecimiento, Guid? exceptoUsuario, Guid? exceptoRol, CancellationToken cancelacion = default);

    void Agregar(Usuario usuario);
}

public interface IRepositorioRoles
{
    Task<Rol?> ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion = default);

    Task<IReadOnlyList<Rol>> ListarAsync(Guid idEstablecimiento, CancellationToken cancelacion = default);
}

public interface IRepositorioSesionesUsuario
{
    void Agregar(SesionUsuario sesion);

    Task<SesionUsuario?> ObtenerPorHuellaAsync(string huella, CancellationToken cancelacion = default);

    Task<IReadOnlyList<SesionUsuario>> ListarVigentesAsync(Guid idUsuario, DateTimeOffset ahora, CancellationToken cancelacion = default);

    /// <summary>Hora de creación de la sesión más reciente de cada usuario; los que nunca iniciaron sesión no aparecen.</summary>
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ObtenerUltimosAccesosAsync(
        IReadOnlyCollection<Guid> idsUsuario, CancellationToken cancelacion = default);
}

public enum ResultadoContrasena
{
    Incorrecta,
    Correcta,
    CorrectaDebeActualizarse,
}

/// <summary>Hash de contraseñas; un hash inválido o desconocido es simplemente una contraseña incorrecta.</summary>
public interface IHasherContrasenas
{
    string Generar(string contrasena);

    ResultadoContrasena Verificar(string resumen, string contrasena);

    /// <summary>Tarda lo mismo que una verificación real, para que los correos desconocidos no se distingan por el tiempo.</summary>
    void VerificarFicticio(string contrasena);
}

/// <summary>Todo lo que necesita una solicitud autenticada, leído en una sola consulta por el hash del token.</summary>
public interface IConsultasSesion
{
    Task<SesionActiva?> ObtenerAsync(string huella, CancellationToken cancelacion = default);
}

public sealed class OpcionesSesion
{
    public TimeSpan Duracion { get; init; } = TimeSpan.FromHours(12);
}
