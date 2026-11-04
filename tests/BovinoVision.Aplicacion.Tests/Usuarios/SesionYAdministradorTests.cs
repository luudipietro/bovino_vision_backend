using BovinoVision.Aplicacion.Tests.Capturas;
using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Usuarios;

namespace BovinoVision.Aplicacion.Tests.Usuarios;

public class SesionYAdministradorTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid IdEstablecimiento = Guid.CreateVersion7();

    private readonly RolesEnMemoria _roles = new();
    private readonly UsuariosEnMemoria _usuarios;
    private readonly SesionesUsuarioEnMemoria _sesiones = new();
    private readonly HasherDePrueba _hasher = new();
    private readonly UnidadDeTrabajoContadora _unidad = new();
    private readonly TiempoControlado _tiempo = new(Ahora);
    private readonly Rol _administrador = new(IdEstablecimiento, "Administrador", [Permiso.RolesGestionar]);

    public SesionYAdministradorTests()
    {
        _usuarios = new UsuariosEnMemoria(_roles);
        _roles.Roles.Add(_administrador);
    }

    [Fact]
    public async Task Cerrar_sesion_revoca_la_sesion_del_token()
    {
        var (token, huella) = TokensDeSesion.Generar();
        var sesion = new SesionUsuario(Guid.CreateVersion7(), huella, Ahora, TimeSpan.FromHours(12));
        _sesiones.Agregar(sesion);

        await new CerrarSesion(_sesiones, _unidad, _tiempo).EjecutarAsync(token, TestContext.Current.CancellationToken);
        await new CerrarSesion(_sesiones, _unidad, _tiempo).EjecutarAsync("token-desconocido", TestContext.Current.CancellationToken);

        Assert.Equal(Ahora, sesion.RevocadaEn);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task El_administrador_inicial_se_crea_una_sola_vez_con_la_contrasena_cifrada()
    {
        var caso = new CrearAdministradorInicial(_usuarios, _roles, _hasher, _unidad);
        var datos = new AdministradorInicial(IdEstablecimiento, _administrador.Id, "Admin", "Admin@Campo.com", "segura123");

        Assert.True(await caso.EjecutarAsync(datos, TestContext.Current.CancellationToken));
        Assert.False(await caso.EjecutarAsync(datos, TestContext.Current.CancellationToken));

        var admin = Assert.Single(_usuarios.Usuarios);
        Assert.Equal("admin@campo.com", admin.Correo);
        Assert.Equal("hash:segura123", admin.ResumenContrasena);
        Assert.Equal(_administrador.Id, admin.IdRol);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task El_administrador_inicial_exige_una_contrasena_de_ocho_caracteres()
    {
        var caso = new CrearAdministradorInicial(_usuarios, _roles, _hasher, _unidad);
        var datos = new AdministradorInicial(IdEstablecimiento, _administrador.Id, "Admin", "admin@campo.com", "corta");

        var error = await Assert.ThrowsAsync<OperacionInvalidaError>(() => caso.EjecutarAsync(datos, TestContext.Current.CancellationToken));

        Assert.Equal("La contraseña debe tener al menos 8 caracteres", error.Message);
        Assert.Empty(_usuarios.Usuarios);
    }
}
