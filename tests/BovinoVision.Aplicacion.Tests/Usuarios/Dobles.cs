using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Dominio.Usuarios;

namespace BovinoVision.Aplicacion.Tests.Usuarios;

/// <summary>Password "correcta" matches any hash "hash:correcta"; hashes starting with "viejo:" ask for a rehash.</summary>
internal sealed class HasherDePrueba : IHasherContrasenas
{
    public int VerificacionesFicticias { get; private set; }

    public string Generar(string contrasena) => $"hash:{contrasena}";

    public ResultadoContrasena Verificar(string resumen, string contrasena) => resumen switch
    {
        _ when resumen == $"hash:{contrasena}" => ResultadoContrasena.Correcta,
        _ when resumen == $"viejo:{contrasena}" => ResultadoContrasena.CorrectaDebeActualizarse,
        _ => ResultadoContrasena.Incorrecta,
    };

    public void VerificarFicticio(string contrasena) => VerificacionesFicticias++;
}

internal sealed class UsuariosEnMemoria(RolesEnMemoria roles) : IRepositorioUsuarios
{
    public List<Usuario> Usuarios { get; } = [];

    public Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken cancelacion = default) =>
        Task.FromResult(Usuarios.SingleOrDefault(u => u.Correo == correo));

    public Task<Usuario?> ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion = default) =>
        Task.FromResult(Usuarios.SingleOrDefault(u => u.Id == id && u.IdEstablecimiento == idEstablecimiento));

    public Task<bool> ExisteCorreoAsync(string correo, Guid? exceptoId, CancellationToken cancelacion = default) =>
        Task.FromResult(Usuarios.Any(u => u.Correo == correo && u.Id != exceptoId));

    public Task<IReadOnlyList<Usuario>> ListarAsync(Guid idEstablecimiento, Guid? idRol, CancellationToken cancelacion = default) =>
        Task.FromResult<IReadOnlyList<Usuario>>(
            [.. Usuarios.Where(u => u.IdEstablecimiento == idEstablecimiento && (idRol is null || u.IdRol == idRol))]);

    public Task<int> ContarAdministradoresActivosAsync(
        Guid idEstablecimiento, Guid? exceptoUsuario, Guid? exceptoRol, CancellationToken cancelacion = default) =>
        Task.FromResult(Usuarios.Count(u =>
            u.IdEstablecimiento == idEstablecimiento && u.Activo && u.Id != exceptoUsuario && u.IdRol != exceptoRol
            && roles.Roles.Single(r => r.Id == u.IdRol).EsAdministrador));

    public void Agregar(Usuario usuario) => Usuarios.Add(usuario);
}

internal sealed class RolesEnMemoria : IRepositorioRoles
{
    public List<Rol> Roles { get; } = [];

    public Task<Rol?> ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion = default) =>
        Task.FromResult(Roles.SingleOrDefault(r => r.Id == id && r.IdEstablecimiento == idEstablecimiento));

    public Task<IReadOnlyList<Rol>> ListarAsync(Guid idEstablecimiento, CancellationToken cancelacion = default) =>
        Task.FromResult<IReadOnlyList<Rol>>([.. Roles.Where(r => r.IdEstablecimiento == idEstablecimiento)]);
}

internal sealed class SesionesUsuarioEnMemoria : IRepositorioSesionesUsuario
{
    public List<SesionUsuario> Sesiones { get; } = [];

    public void Agregar(SesionUsuario sesion) => Sesiones.Add(sesion);

    public Task<SesionUsuario?> ObtenerPorHuellaAsync(string huella, CancellationToken cancelacion = default) =>
        Task.FromResult(Sesiones.SingleOrDefault(s => s.Huella == huella));

    public Task<IReadOnlyList<SesionUsuario>> ListarVigentesAsync(
        Guid idUsuario, DateTimeOffset ahora, CancellationToken cancelacion = default) =>
        Task.FromResult<IReadOnlyList<SesionUsuario>>([.. Sesiones.Where(s => s.IdUsuario == idUsuario && s.EstaVigente(ahora))]);

    public Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ObtenerUltimosAccesosAsync(
        IReadOnlyCollection<Guid> idsUsuario, CancellationToken cancelacion = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, DateTimeOffset>>(Sesiones
            .Where(s => idsUsuario.Contains(s.IdUsuario))
            .GroupBy(s => s.IdUsuario)
            .ToDictionary(g => g.Key, g => g.Max(s => s.CreadaEn)));
}
