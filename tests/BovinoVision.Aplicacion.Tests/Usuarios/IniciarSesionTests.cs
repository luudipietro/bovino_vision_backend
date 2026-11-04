using BovinoVision.Aplicacion.Tests.Capturas;
using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Usuarios;

namespace BovinoVision.Aplicacion.Tests.Usuarios;

public class IniciarSesionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid IdEstablecimiento = Guid.CreateVersion7();

    private readonly RolesEnMemoria _roles = new();
    private readonly UsuariosEnMemoria _usuarios;
    private readonly SesionesUsuarioEnMemoria _sesiones = new();
    private readonly HasherDePrueba _hasher = new();
    private readonly UnidadDeTrabajoContadora _unidad = new();
    private readonly TiempoControlado _tiempo = new(Ahora);
    private readonly Usuario _usuario;

    public IniciarSesionTests()
    {
        _usuarios = new UsuariosEnMemoria(_roles);
        var rol = new Rol(IdEstablecimiento, "Operario de Campo", [Permiso.CapturasRegistrar, Permiso.RodeoConsultar]);
        _roles.Roles.Add(rol);
        _usuario = new Usuario(IdEstablecimiento, rol.Id, "Ana", "ana@campo.com", "hash:correcta");
        _usuarios.Agregar(_usuario);
    }

    private IniciarSesion CasoDeUso() =>
        new(_usuarios, _roles, _sesiones, _hasher, _unidad, _tiempo, new OpcionesSesion { Duracion = TimeSpan.FromHours(12) });

    private Task<SesionIniciada> Ingresar(string correo = "ana@campo.com", string contrasena = "correcta") =>
        CasoDeUso().EjecutarAsync(new CredencialesUsuario(correo, contrasena), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Con_credenciales_correctas_abre_una_sesion_que_guarda_solo_la_huella_del_token()
    {
        var iniciada = await Ingresar(correo: " ANA@campo.com ");

        var sesion = Assert.Single(_sesiones.Sesiones);
        Assert.Equal(_usuario.Id, sesion.IdUsuario);
        Assert.NotEqual(iniciada.Token, sesion.Huella);
        Assert.Equal(TokensDeSesion.Huella(iniciada.Token), sesion.Huella);
        Assert.True(iniciada.Token.Length >= 43);
        Assert.Equal(Ahora.AddHours(12), iniciada.Sesion.ExpiraEn);
        Assert.Equal("Ana", iniciada.Sesion.Usuario.Nombre);
        Assert.Equal("Operario de Campo", iniciada.Sesion.Usuario.Rol);
        Assert.Equal([Permiso.CapturasRegistrar, Permiso.RodeoConsultar], iniciada.Sesion.Permisos);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Un_correo_desconocido_responde_lo_mismo_que_una_contrasena_incorrecta()
    {
        var desconocido = await Assert.ThrowsAsync<NoAutenticadoError>(() => Ingresar(correo: "nadie@campo.com"));
        var incorrecta = await Assert.ThrowsAsync<NoAutenticadoError>(() => Ingresar(contrasena: "otra"));

        Assert.Equal("Usuario o contraseña incorrectos.", desconocido.Message);
        Assert.Equal(desconocido.Message, incorrecta.Message);
        Assert.Equal(1, _hasher.VerificacionesFicticias);
        Assert.Equal(1, _usuario.IntentosFallidos);
        Assert.Empty(_sesiones.Sesiones);
    }

    [Fact]
    public async Task El_quinto_intento_fallido_bloquea_y_el_bloqueo_rechaza_incluso_la_contrasena_correcta()
    {
        for (var intento = 1; intento < Usuario.MaximoIntentosFallidos; intento++)
        {
            await Assert.ThrowsAsync<NoAutenticadoError>(() => Ingresar(contrasena: "otra"));
        }

        var quinto = await Assert.ThrowsAsync<UsuarioBloqueadoError>(() => Ingresar(contrasena: "otra"));
        _tiempo.Ahora = Ahora.AddMinutes(2);
        var bloqueado = await Assert.ThrowsAsync<UsuarioBloqueadoError>(() => Ingresar());

        Assert.Equal("El usuario está bloqueado por intentos fallidos. Volvé a intentar en 5 minutos.", quinto.Message);
        Assert.Equal(TimeSpan.FromMinutes(3), bloqueado.Restante);
        Assert.Empty(_sesiones.Sesiones);

        _tiempo.Ahora = Ahora.AddMinutes(5);
        await Ingresar();
        Assert.Single(_sesiones.Sesiones);
        Assert.Equal(0, _usuario.IntentosFallidos);
    }

    [Fact]
    public async Task Una_cuenta_dada_de_baja_solo_se_informa_con_la_contrasena_correcta()
    {
        _usuario.DarDeBaja();

        var conCorrecta = await Assert.ThrowsAsync<AccesoDenegadoError>(() => Ingresar());
        await Assert.ThrowsAsync<NoAutenticadoError>(() => Ingresar(contrasena: "otra"));

        Assert.Equal("No es posible acceder con esta cuenta. Contactá a un administrador.", conCorrecta.Message);
        Assert.Empty(_sesiones.Sesiones);
    }

    [Fact]
    public async Task Una_contrasena_con_resumen_viejo_se_vuelve_a_cifrar()
    {
        _usuario.CambiarContrasena("viejo:correcta");

        await Ingresar();

        Assert.Equal("hash:correcta", _usuario.ResumenContrasena);
    }
}
