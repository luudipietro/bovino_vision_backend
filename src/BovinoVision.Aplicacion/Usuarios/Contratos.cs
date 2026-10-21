using BovinoVision.Dominio.Usuarios;

namespace BovinoVision.Aplicacion.Usuarios;

public interface IRepositorioUsuarios
{
    /// <summary>The email must already be normalized with <see cref="Usuario.NormalizarCorreo"/>.</summary>
    Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken cancelacion = default);

    Task<Usuario?> ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion = default);

    Task<bool> ExisteCorreoAsync(string correo, Guid? exceptoId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<Usuario>> ListarAsync(Guid idEstablecimiento, Guid? idRol, CancellationToken cancelacion = default);

    /// <summary>Active users whose role can manage roles, leaving out one user or one role.</summary>
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
}

public enum ResultadoContrasena
{
    Incorrecta,
    Correcta,
    CorrectaDebeActualizarse,
}

/// <summary>Password hashing; an invalid or unknown hash is just an incorrect password.</summary>
public interface IHasherContrasenas
{
    string Generar(string contrasena);

    ResultadoContrasena Verificar(string resumen, string contrasena);

    /// <summary>Spends the same time as a real check, so unknown emails cannot be told apart by timing.</summary>
    void VerificarFicticio(string contrasena);
}

/// <summary>Everything an authenticated request needs, read in a single query by token hash.</summary>
public interface IConsultasSesion
{
    Task<SesionActiva?> ObtenerAsync(string huella, CancellationToken cancelacion = default);
}

public sealed class OpcionesSesion
{
    public TimeSpan Duracion { get; init; } = TimeSpan.FromHours(12);
}
