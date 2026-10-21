using BovinoVision.Aplicacion.Tests.Capturas;
using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Usuarios;

namespace BovinoVision.Aplicacion.Tests.Usuarios;

public class GestionUsuariosTests
{
    private const string SinAdministrador = "No puede quedar el sistema sin un administrador";
    private static readonly DateTimeOffset Ahora = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly RolesEnMemoria _roles = new();
    private readonly UsuariosEnMemoria _usuarios;
    private readonly SesionesUsuarioEnMemoria _sesiones = new();
    private readonly HasherDePrueba _hasher = new();
    private readonly UnidadDeTrabajoContadora _unidad = new();
    private readonly TiempoControlado _tiempo = new(Ahora);
    private readonly ContextoUsuarioFijo _contexto = new();
    private readonly Rol _administrador;
    private readonly Rol _operario;
    private readonly Usuario _admin;

    public GestionUsuariosTests()
    {
        _usuarios = new UsuariosEnMemoria(_roles);
        _administrador = new Rol(_contexto.IdEstablecimiento, "Administrador", [Permiso.UsuariosGestionar, Permiso.RolesGestionar]);
        _operario = new Rol(_contexto.IdEstablecimiento, "Operario de Campo", [Permiso.CapturasRegistrar]);
        _roles.Roles.AddRange([_administrador, _operario]);
        _admin = new Usuario(_contexto.IdEstablecimiento, _administrador.Id, "Admin", "admin@campo.com", "hash:x");
        _usuarios.Agregar(_admin);
    }

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private RegistrarUsuario Registrar() => new(_usuarios, _roles, _hasher, _unidad, _contexto);

    private ModificarUsuario Modificar() => new(_usuarios, _roles, _sesiones, _hasher, _unidad, _contexto, _tiempo);

    private AsignarRolUsuario AsignarRol() => new(_usuarios, _roles, _unidad, _contexto);

    private DarDeBajaUsuario DarDeBaja() => new(_usuarios, _roles, _sesiones, _unidad, _contexto, _tiempo);

    private DefinirPermisosRol DefinirPermisos() => new(_roles, _usuarios, _unidad, _contexto);

    private Usuario AgregarUsuario(Rol rol, string correo = "ana@campo.com")
    {
        var usuario = new Usuario(_contexto.IdEstablecimiento, rol.Id, "Ana", correo, "hash:x");
        _usuarios.Agregar(usuario);
        return usuario;
    }

    [Fact]
    public async Task Registrar_crea_un_usuario_activo_con_la_contrasena_cifrada()
    {
        var creado = await Registrar().EjecutarAsync(
            new NuevoUsuario("Ana", "Ana@Campo.com", "segura123", _operario.Id), Cancelacion);

        Assert.Equal("ana@campo.com", creado.Correo);
        Assert.Equal("Operario de Campo", creado.Rol);
        Assert.True(creado.Activo);
        Assert.Equal("hash:segura123", _usuarios.Usuarios.Single(u => u.Id == creado.Id).ResumenContrasena);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Registrar_rechaza_un_correo_repetido_una_contrasena_corta_y_un_rol_inexistente()
    {
        var repetido = await Assert.ThrowsAsync<ConflictoError>(() =>
            Registrar().EjecutarAsync(new NuevoUsuario("Otro", "ADMIN@campo.com", "segura123", _operario.Id), Cancelacion));
        var corta = await Assert.ThrowsAsync<OperacionInvalidaError>(() =>
            Registrar().EjecutarAsync(new NuevoUsuario("Otro", "otro@campo.com", "corta", _operario.Id), Cancelacion));
        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            Registrar().EjecutarAsync(new NuevoUsuario("Otro", "otro@campo.com", "segura123", Guid.CreateVersion7()), Cancelacion));

        Assert.Equal("Ya existe un usuario con el correo admin@campo.com", repetido.Message);
        Assert.Equal("La contraseña debe tener al menos 8 caracteres", corta.Message);
        Assert.Single(_usuarios.Usuarios);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task Modificar_cambia_solo_lo_indicado_y_una_nueva_contrasena_cierra_sus_sesiones()
    {
        var ana = AgregarUsuario(_operario);
        var sesion = new SesionUsuario(ana.Id, "huella", Ahora, TimeSpan.FromHours(12));
        _sesiones.Agregar(sesion);

        var modificado = await Modificar().EjecutarAsync(ana.Id, new CambiosUsuario(null, "ana.p@campo.com", "nueva-clave"), Cancelacion);

        Assert.Equal("Ana", modificado.Nombre);
        Assert.Equal("ana.p@campo.com", modificado.Correo);
        Assert.Equal("hash:nueva-clave", ana.ResumenContrasena);
        Assert.Equal(Ahora, sesion.RevocadaEn);
        var repetido = await Assert.ThrowsAsync<ConflictoError>(() =>
            Modificar().EjecutarAsync(ana.Id, new CambiosUsuario(null, "admin@campo.com", null), Cancelacion));
        Assert.Equal("Ya existe un usuario con el correo admin@campo.com", repetido.Message);
    }

    [Fact]
    public async Task El_unico_administrador_no_puede_perder_el_rol_ni_darse_de_baja()
    {
        var cambio = await Assert.ThrowsAsync<OperacionInvalidaError>(() =>
            AsignarRol().EjecutarAsync(_admin.Id, new CambioRol(_operario.Id), Cancelacion));
        var baja = await Assert.ThrowsAsync<OperacionInvalidaError>(() => DarDeBaja().EjecutarAsync(_admin.Id, Cancelacion));

        Assert.Equal(SinAdministrador, cambio.Message);
        Assert.Equal(SinAdministrador, baja.Message);
        Assert.Equal(_administrador.Id, _admin.IdRol);
        Assert.True(_admin.Activo);

        AgregarUsuario(_administrador, "otro.admin@campo.com");
        await AsignarRol().EjecutarAsync(_admin.Id, new CambioRol(_operario.Id), Cancelacion);
        Assert.Equal(_operario.Id, _admin.IdRol);
    }

    [Fact]
    public async Task Dar_de_baja_es_logico_y_cierra_las_sesiones_del_usuario()
    {
        var ana = AgregarUsuario(_operario);
        var sesion = new SesionUsuario(ana.Id, "huella", Ahora, TimeSpan.FromHours(12));
        _sesiones.Agregar(sesion);

        var baja = await DarDeBaja().EjecutarAsync(ana.Id, Cancelacion);

        Assert.False(baja.Activo);
        Assert.Contains(ana, _usuarios.Usuarios);
        Assert.Equal(Ahora, sesion.RevocadaEn);
    }

    [Fact]
    public async Task Los_permisos_del_rol_no_pueden_dejar_el_sistema_sin_administrador()
    {
        var error = await Assert.ThrowsAsync<OperacionInvalidaError>(() =>
            DefinirPermisos().EjecutarAsync(_administrador.Id, new PermisosRol([Permiso.UsuariosGestionar]), Cancelacion));
        var operario = await DefinirPermisos().EjecutarAsync(
            _operario.Id, new PermisosRol([Permiso.CapturasRegistrar, Permiso.RodeoConsultar]), Cancelacion);

        Assert.Equal(SinAdministrador, error.Message);
        Assert.True(_administrador.EsAdministrador);
        Assert.Equal([Permiso.CapturasRegistrar, Permiso.RodeoConsultar], operario.Permisos);
    }

    [Fact]
    public async Task El_listado_de_roles_informa_cuantos_usuarios_tiene_cada_uno()
    {
        AgregarUsuario(_operario);
        AgregarUsuario(_operario, "beto@campo.com");

        var roles = await new ListarRoles(_roles, _usuarios, _contexto).EjecutarAsync(Cancelacion);

        Assert.Equal(1, roles.Single(r => r.Id == _administrador.Id).CantidadUsuarios);
        Assert.Equal(2, roles.Single(r => r.Id == _operario.Id).CantidadUsuarios);
        Assert.Equal(Enum.GetValues<Permiso>().Length, new ListarPermisos().Ejecutar().Count);
    }
}
