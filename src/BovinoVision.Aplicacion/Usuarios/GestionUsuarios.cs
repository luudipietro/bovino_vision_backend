using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Usuarios;

namespace BovinoVision.Aplicacion.Usuarios;

public sealed record NuevoUsuario(string Nombre, string Correo, string Contrasena, Guid IdRol);

/// <summary>Null values keep the current data.</summary>
public sealed record CambiosUsuario(string? Nombre = null, string? Correo = null, string? Contrasena = null);

public sealed record CambioRol(Guid IdRol);

public sealed record PermisosRol(IReadOnlyList<Permiso> Permisos);

/// <summary>Shared checks of the user management use cases.</summary>
internal static class ReglasUsuarios
{
    /// <summary>CU-05 5a, CU-07 3a and CU-08 4a: an operation cannot leave the system without an administrator.</summary>
    public static async Task ExigirOtroAdministradorAsync(
        IRepositorioUsuarios usuarios, Guid idEstablecimiento, Guid? exceptoUsuario, Guid? exceptoRol, CancellationToken cancelacion)
    {
        if (await usuarios.ContarAdministradoresActivosAsync(idEstablecimiento, exceptoUsuario, exceptoRol, cancelacion) == 0)
        {
            throw new OperacionInvalidaError("No puede quedar el sistema sin un administrador");
        }
    }

    public static async Task ExigirCorreoLibreAsync(
        IRepositorioUsuarios usuarios, string correo, Guid? exceptoId, CancellationToken cancelacion)
    {
        var normalizado = Usuario.NormalizarCorreo(correo);
        if (await usuarios.ExisteCorreoAsync(normalizado, exceptoId, cancelacion))
        {
            throw new ConflictoError($"Ya existe un usuario con el correo {normalizado}");
        }
    }

    public static async Task<Rol> ObtenerRolAsync(IRepositorioRoles roles, Guid idRol, Guid idEstablecimiento, CancellationToken cancelacion) =>
        await roles.ObtenerAsync(idRol, idEstablecimiento, cancelacion) ?? throw NoEncontradoError.De("el rol", idRol);

    public static async Task<Usuario> ObtenerUsuarioAsync(
        IRepositorioUsuarios usuarios, Guid idUsuario, Guid idEstablecimiento, CancellationToken cancelacion) =>
        await usuarios.ObtenerAsync(idUsuario, idEstablecimiento, cancelacion) ?? throw NoEncontradoError.De("el usuario", idUsuario);

    public static async Task CerrarSesionesAsync(
        IRepositorioSesionesUsuario sesiones, Guid idUsuario, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        foreach (var sesion in await sesiones.ListarVigentesAsync(idUsuario, ahora, cancelacion))
        {
            sesion.Revocar(ahora);
        }
    }
}

/// <summary>CU-06: users of the establecimiento, optionally filtered by role.</summary>
public sealed class ListarUsuarios(IRepositorioUsuarios usuarios, IRepositorioRoles roles, IContextoUsuario contexto)
{
    public async Task<IReadOnlyList<UsuarioRespuesta>> EjecutarAsync(Guid? idRol, CancellationToken cancelacion = default)
    {
        var porId = (await roles.ListarAsync(contexto.IdEstablecimiento, cancelacion)).ToDictionary(r => r.Id);
        var lista = await usuarios.ListarAsync(contexto.IdEstablecimiento, idRol, cancelacion);
        return [.. lista.Select(u => UsuarioRespuesta.Desde(u, porId[u.IdRol]))];
    }
}

/// <summary>CU-03: the new user starts active with a hashed password.</summary>
public sealed class RegistrarUsuario(
    IRepositorioUsuarios usuarios,
    IRepositorioRoles roles,
    IHasherContrasenas hasher,
    IUnidadDeTrabajo unidadDeTrabajo,
    IContextoUsuario contexto)
{
    public async Task<UsuarioRespuesta> EjecutarAsync(NuevoUsuario datos, CancellationToken cancelacion = default)
    {
        Contrasenas.Validar(datos.Contrasena);
        var rol = await ReglasUsuarios.ObtenerRolAsync(roles, datos.IdRol, contexto.IdEstablecimiento, cancelacion);
        var usuario = new Usuario(contexto.IdEstablecimiento, rol.Id, datos.Nombre, datos.Correo, hasher.Generar(datos.Contrasena));
        await ReglasUsuarios.ExigirCorreoLibreAsync(usuarios, usuario.Correo, null, cancelacion);
        usuarios.Agregar(usuario);
        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        return UsuarioRespuesta.Desde(usuario, rol);
    }
}

/// <summary>CU-04: name, email or password; a new password closes the user's open sessions.</summary>
public sealed class ModificarUsuario(
    IRepositorioUsuarios usuarios,
    IRepositorioRoles roles,
    IRepositorioSesionesUsuario sesiones,
    IHasherContrasenas hasher,
    IUnidadDeTrabajo unidadDeTrabajo,
    IContextoUsuario contexto,
    TimeProvider tiempo)
{
    public async Task<UsuarioRespuesta> EjecutarAsync(Guid idUsuario, CambiosUsuario cambios, CancellationToken cancelacion = default)
    {
        var usuario = await ReglasUsuarios.ObtenerUsuarioAsync(usuarios, idUsuario, contexto.IdEstablecimiento, cancelacion);
        if (cambios.Correo is not null)
        {
            await ReglasUsuarios.ExigirCorreoLibreAsync(usuarios, cambios.Correo, usuario.Id, cancelacion);
        }

        usuario.ModificarDatos(cambios.Nombre ?? usuario.Nombre, cambios.Correo ?? usuario.Correo);
        if (cambios.Contrasena is not null)
        {
            Contrasenas.Validar(cambios.Contrasena);
            usuario.CambiarContrasena(hasher.Generar(cambios.Contrasena));
            await ReglasUsuarios.CerrarSesionesAsync(sesiones, usuario.Id, tiempo.GetUtcNow(), cancelacion);
        }

        var rol = await ReglasUsuarios.ObtenerRolAsync(roles, usuario.IdRol, contexto.IdEstablecimiento, cancelacion);
        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        return UsuarioRespuesta.Desde(usuario, rol);
    }
}

/// <summary>CU-07: the new role applies on the user's next request.</summary>
public sealed class AsignarRolUsuario(
    IRepositorioUsuarios usuarios,
    IRepositorioRoles roles,
    IUnidadDeTrabajo unidadDeTrabajo,
    IContextoUsuario contexto)
{
    public async Task<UsuarioRespuesta> EjecutarAsync(Guid idUsuario, CambioRol cambio, CancellationToken cancelacion = default)
    {
        var usuario = await ReglasUsuarios.ObtenerUsuarioAsync(usuarios, idUsuario, contexto.IdEstablecimiento, cancelacion);
        var actual = await ReglasUsuarios.ObtenerRolAsync(roles, usuario.IdRol, contexto.IdEstablecimiento, cancelacion);
        var nuevo = await ReglasUsuarios.ObtenerRolAsync(roles, cambio.IdRol, contexto.IdEstablecimiento, cancelacion);
        // Rules are checked before changing the user, so a rejected change leaves it untouched.
        if (usuario.Activo && actual.EsAdministrador && !nuevo.EsAdministrador)
        {
            await ReglasUsuarios.ExigirOtroAdministradorAsync(usuarios, contexto.IdEstablecimiento, usuario.Id, null, cancelacion);
        }

        usuario.AsignarRol(nuevo.Id);

        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        return UsuarioRespuesta.Desde(usuario, nuevo);
    }
}

/// <summary>CU-05: logical deactivation; the user's sessions stop working at once.</summary>
public sealed class DarDeBajaUsuario(
    IRepositorioUsuarios usuarios,
    IRepositorioRoles roles,
    IRepositorioSesionesUsuario sesiones,
    IUnidadDeTrabajo unidadDeTrabajo,
    IContextoUsuario contexto,
    TimeProvider tiempo)
{
    public async Task<UsuarioRespuesta> EjecutarAsync(Guid idUsuario, CancellationToken cancelacion = default)
    {
        var usuario = await ReglasUsuarios.ObtenerUsuarioAsync(usuarios, idUsuario, contexto.IdEstablecimiento, cancelacion);
        var rol = await ReglasUsuarios.ObtenerRolAsync(roles, usuario.IdRol, contexto.IdEstablecimiento, cancelacion);
        if (usuario.Activo && rol.EsAdministrador)
        {
            await ReglasUsuarios.ExigirOtroAdministradorAsync(usuarios, contexto.IdEstablecimiento, usuario.Id, null, cancelacion);
        }

        usuario.DarDeBaja();

        await ReglasUsuarios.CerrarSesionesAsync(sesiones, usuario.Id, tiempo.GetUtcNow(), cancelacion);
        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        return UsuarioRespuesta.Desde(usuario, rol);
    }
}

public sealed class ListarRoles(IRepositorioRoles roles, IRepositorioUsuarios usuarios, IContextoUsuario contexto)
{
    public async Task<IReadOnlyList<RolRespuesta>> EjecutarAsync(CancellationToken cancelacion = default)
    {
        var lista = await roles.ListarAsync(contexto.IdEstablecimiento, cancelacion);
        var porRol = (await usuarios.ListarAsync(contexto.IdEstablecimiento, null, cancelacion))
            .GroupBy(u => u.IdRol)
            .ToDictionary(g => g.Key, g => g.Count());
        return [.. lista.Select(r => new RolRespuesta(r.Id, r.Nombre, r.Permisos, porRol.GetValueOrDefault(r.Id)))];
    }
}

/// <summary>Catalog of permissions with the functionality each one enables.</summary>
public sealed class ListarPermisos
{
    private readonly IReadOnlyList<PermisoRespuesta> _catalogo =
    [
        new(Permiso.CapturasRegistrar, "Registrar capturas y pesajes"),
        new(Permiso.RodeoGestionar, "Dar de alta y modificar animales y lotes"),
        new(Permiso.RodeoConsultar, "Consultar animales, lotes, historial y alertas de un animal"),
        new(Permiso.AnaliticaConsultar, "Consultar alertas del rodeo, panel y reportes"),
        new(Permiso.UsuariosGestionar, "Gestionar usuarios"),
        new(Permiso.RolesGestionar, "Gestionar roles y permisos"),
        new(Permiso.AuditoriaConsultar, "Consultar la auditoría"),
    ];

    public IReadOnlyList<PermisoRespuesta> Ejecutar() => _catalogo;
}

/// <summary>CU-08: the permissions apply to every user of the role on their next request.</summary>
public sealed class DefinirPermisosRol(
    IRepositorioRoles roles,
    IRepositorioUsuarios usuarios,
    IUnidadDeTrabajo unidadDeTrabajo,
    IContextoUsuario contexto)
{
    public async Task<RolRespuesta> EjecutarAsync(Guid idRol, PermisosRol permisos, CancellationToken cancelacion = default)
    {
        var rol = await ReglasUsuarios.ObtenerRolAsync(roles, idRol, contexto.IdEstablecimiento, cancelacion);
        if (rol.EsAdministrador && !permisos.Permisos.Contains(Permiso.RolesGestionar))
        {
            await ReglasUsuarios.ExigirOtroAdministradorAsync(usuarios, contexto.IdEstablecimiento, null, rol.Id, cancelacion);
        }

        rol.DefinirPermisos(permisos.Permisos);

        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        var asignados = (await usuarios.ListarAsync(contexto.IdEstablecimiento, rol.Id, cancelacion)).Count;
        return new RolRespuesta(rol.Id, rol.Nombre, rol.Permisos, asignados);
    }
}
