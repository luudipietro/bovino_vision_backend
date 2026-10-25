using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Dominio.Usuarios;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace BovinoVision.Infraestructura.Usuarios;

internal sealed class RepositorioUsuarios(BovinoVisionDbContext contexto) : IRepositorioUsuarios
{
    public Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken cancelacion = default) =>
        contexto.Usuarios.SingleOrDefaultAsync(u => u.Correo == correo, cancelacion);

    public Task<Usuario?> ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion = default) =>
        contexto.Usuarios.SingleOrDefaultAsync(u => u.Id == id && u.IdEstablecimiento == idEstablecimiento, cancelacion);

    public Task<bool> ExisteCorreoAsync(string correo, Guid? exceptoId, CancellationToken cancelacion = default) =>
        contexto.Usuarios.AnyAsync(u => u.Correo == correo && u.Id != exceptoId, cancelacion);

    public async Task<IReadOnlyList<Usuario>> ListarAsync(Guid idEstablecimiento, Guid? idRol, CancellationToken cancelacion = default) =>
        await contexto.Usuarios
            .Where(u => u.IdEstablecimiento == idEstablecimiento && (idRol == null || u.IdRol == idRol))
            .OrderBy(u => u.Nombre)
            .ToListAsync(cancelacion);

    public Task<int> ContarAdministradoresActivosAsync(
        Guid idEstablecimiento, Guid? exceptoUsuario, Guid? exceptoRol, CancellationToken cancelacion = default) =>
        contexto.Usuarios
            .Where(u => u.IdEstablecimiento == idEstablecimiento && u.Activo && u.Id != exceptoUsuario && u.IdRol != exceptoRol)
            .Join(contexto.Roles, u => u.IdRol, r => r.Id, (_, r) => r)
            .CountAsync(r => r.Permisos.Contains(Permiso.RolesGestionar), cancelacion);

    public void Agregar(Usuario usuario) => contexto.Usuarios.Add(usuario);
}

internal sealed class RepositorioRoles(BovinoVisionDbContext contexto) : IRepositorioRoles
{
    public Task<Rol?> ObtenerAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion = default) =>
        contexto.Roles.SingleOrDefaultAsync(r => r.Id == id && r.IdEstablecimiento == idEstablecimiento, cancelacion);

    public async Task<IReadOnlyList<Rol>> ListarAsync(Guid idEstablecimiento, CancellationToken cancelacion = default) =>
        await contexto.Roles.Where(r => r.IdEstablecimiento == idEstablecimiento).OrderBy(r => r.Nombre).ToListAsync(cancelacion);
}

internal sealed class RepositorioSesionesUsuario(BovinoVisionDbContext contexto) : IRepositorioSesionesUsuario
{
    public void Agregar(SesionUsuario sesion) => contexto.SesionesUsuario.Add(sesion);

    public Task<SesionUsuario?> ObtenerPorHuellaAsync(string huella, CancellationToken cancelacion = default) =>
        contexto.SesionesUsuario.SingleOrDefaultAsync(s => s.Huella == huella, cancelacion);

    public async Task<IReadOnlyList<SesionUsuario>> ListarVigentesAsync(
        Guid idUsuario, DateTimeOffset ahora, CancellationToken cancelacion = default) =>
        await contexto.SesionesUsuario
            .Where(s => s.IdUsuario == idUsuario && s.RevocadaEn == null && s.ExpiraEn > ahora)
            .ToListAsync(cancelacion);

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ObtenerUltimosAccesosAsync(
        IReadOnlyCollection<Guid> idsUsuario, CancellationToken cancelacion = default) =>
        await contexto.SesionesUsuario.AsNoTracking()
            .Where(s => idsUsuario.Contains(s.IdUsuario))
            .GroupBy(s => s.IdUsuario)
            .Select(g => new { IdUsuario = g.Key, UltimoAcceso = g.Max(s => s.CreadaEn) })
            .ToDictionaryAsync(x => x.IdUsuario, x => x.UltimoAcceso, cancelacion);
}

internal sealed class ConsultasSesion(BovinoVisionDbContext contexto) : IConsultasSesion
{
    public Task<SesionActiva?> ObtenerAsync(string huella, CancellationToken cancelacion = default) =>
        (from sesion in contexto.SesionesUsuario.AsNoTracking()
         join usuario in contexto.Usuarios on sesion.IdUsuario equals usuario.Id
         join rol in contexto.Roles on usuario.IdRol equals rol.Id
         where sesion.Huella == huella
         select new SesionActiva(
             sesion.Id,
             usuario.IdEstablecimiento,
             sesion.ExpiraEn,
             sesion.RevocadaEn,
             new UsuarioRespuesta(usuario.Id, usuario.Nombre, usuario.Correo, rol.Id, rol.Nombre, usuario.Activo, usuario.BloqueadoHasta,
                 contexto.SesionesUsuario.Where(s => s.IdUsuario == usuario.Id).Max(s => (DateTimeOffset?)s.CreadaEn)),
             rol.Permisos))
        .SingleOrDefaultAsync(cancelacion);
}
