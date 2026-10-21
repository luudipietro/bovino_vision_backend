using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Usuarios;

namespace BovinoVision.Dominio.Tests.Usuarios;

public class UsuarioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid IdEstablecimiento = Guid.CreateVersion7();
    private static readonly Guid IdRol = Guid.CreateVersion7();

    private static Usuario NuevoUsuario() =>
        new(IdEstablecimiento, IdRol, "  Ana Pérez ", " Ana@Campo.COM ", "hash");

    [Fact]
    public void Un_usuario_nuevo_queda_activo_con_nombre_y_correo_normalizados()
    {
        var usuario = NuevoUsuario();

        Assert.True(usuario.Activo);
        Assert.Equal("Ana Pérez", usuario.Nombre);
        Assert.Equal("ana@campo.com", usuario.Correo);
        Assert.Equal(0, usuario.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
    }

    [Theory]
    [InlineData("", "ana@campo.com", "El nombre del usuario es obligatorio")]
    [InlineData("Ana", "sin-arroba", "El correo electrónico no es válido")]
    [InlineData("Ana", " ", "El correo electrónico no es válido")]
    public void Nombre_y_correo_son_obligatorios(string nombre, string correo, string mensaje)
    {
        var error = Assert.Throws<OperacionInvalidaError>(() => new Usuario(IdEstablecimiento, IdRol, nombre, correo, "hash"));

        Assert.Equal(mensaje, error.Message);
    }

    [Fact]
    public void El_quinto_intento_fallido_consecutivo_bloquea_cinco_minutos()
    {
        var usuario = NuevoUsuario();

        for (var intento = 1; intento < Usuario.MaximoIntentosFallidos; intento++)
        {
            usuario.RegistrarIntentoFallido(Ahora);
            Assert.False(usuario.EstaBloqueado(Ahora));
        }
        usuario.RegistrarIntentoFallido(Ahora);

        Assert.True(usuario.EstaBloqueado(Ahora));
        Assert.Equal(Ahora + TimeSpan.FromMinutes(5), usuario.BloqueadoHasta);
        Assert.Equal(TimeSpan.FromMinutes(5), usuario.TiempoRestanteDeBloqueo(Ahora));
        Assert.True(usuario.EstaBloqueado(Ahora.AddMinutes(5).AddTicks(-1)));
        Assert.False(usuario.EstaBloqueado(Ahora.AddMinutes(5)));
    }

    [Fact]
    public void Un_ingreso_correcto_reinicia_los_intentos()
    {
        var usuario = NuevoUsuario();
        usuario.RegistrarIntentoFallido(Ahora);
        usuario.RegistrarIntentoFallido(Ahora);

        usuario.RegistrarIngresoExitoso();
        for (var intento = 1; intento < Usuario.MaximoIntentosFallidos; intento++)
        {
            usuario.RegistrarIntentoFallido(Ahora);
        }

        Assert.False(usuario.EstaBloqueado(Ahora));
    }

    [Fact]
    public void Vencido_el_bloqueo_se_vuelven_a_contar_cinco_intentos()
    {
        var usuario = NuevoUsuario();
        for (var intento = 0; intento < Usuario.MaximoIntentosFallidos; intento++)
        {
            usuario.RegistrarIntentoFallido(Ahora);
        }
        var despues = Ahora.AddMinutes(6);

        usuario.RegistrarIntentoFallido(despues);

        Assert.False(usuario.EstaBloqueado(despues));
        Assert.Equal(1, usuario.IntentosFallidos);
    }

    [Fact]
    public void Asignar_el_mismo_rol_se_rechaza()
    {
        var usuario = NuevoUsuario();

        var error = Assert.Throws<OperacionInvalidaError>(() => usuario.AsignarRol(IdRol));

        Assert.Equal("El usuario ya tiene ese rol", error.Message);
        var otroRol = Guid.CreateVersion7();
        usuario.AsignarRol(otroRol);
        Assert.Equal(otroRol, usuario.IdRol);
    }

    [Fact]
    public void La_baja_es_logica_y_no_se_repite()
    {
        var usuario = NuevoUsuario();

        usuario.DarDeBaja();

        Assert.False(usuario.Activo);
        var error = Assert.Throws<OperacionInvalidaError>(usuario.DarDeBaja);
        Assert.Equal("El usuario ya está dado de baja", error.Message);
    }

    [Fact]
    public void Modificar_datos_normaliza_y_valida()
    {
        var usuario = NuevoUsuario();

        usuario.ModificarDatos(" Ana María ", "ANA.MARIA@campo.com");

        Assert.Equal("Ana María", usuario.Nombre);
        Assert.Equal("ana.maria@campo.com", usuario.Correo);
        Assert.Throws<OperacionInvalidaError>(() => usuario.ModificarDatos("Ana", "invalido"));
    }
}
