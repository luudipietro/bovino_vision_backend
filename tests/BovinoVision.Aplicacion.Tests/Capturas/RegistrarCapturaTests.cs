using System.Security.Cryptography;
using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Trazabilidad;

namespace BovinoVision.Aplicacion.Tests.Capturas;

public class RegistrarCapturaTests
{
    private static readonly byte[] ImagenValida = "contenido-de-prueba"u8.ToArray();
    private static readonly DateTimeOffset Ahora = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly SesionesEnMemoria _sesiones = new();
    private readonly CapturasEnMemoria _capturas = new();
    private readonly AnimalesEnMemoria _animales = new();
    private readonly AlmacenamientoEnMemoria _almacenamiento = new();
    private readonly UnidadDeTrabajoContadora _unidad = new();
    private readonly ContextoUsuarioFijo _usuario = new();
    private readonly TiempoControlado _tiempo = new(Ahora);

    private RegistrarCaptura CasoDeUso() =>
        new(_sesiones, _capturas, _animales, _almacenamiento, _unidad, _usuario, _tiempo);

    private (SesionCaptura Sesion, Animal Animal) SesionYAnimal()
    {
        var sesion = new SesionCaptura(_usuario.IdUsuario, Ahora);
        _sesiones.Agregar(sesion);
        var animal = new Animal(_usuario.IdEstablecimiento, "AR001");
        _animales.Animales.Add(animal);
        return (sesion, animal);
    }

    private static RegistrarCapturaComando Comando(
        SesionCaptura sesion,
        Animal animal,
        string tipoContenido = "image/jpeg",
        byte[]? contenido = null,
        DateTimeOffset? fechaCaptura = null) =>
        new(sesion.Id, animal.Id, "foto.jpg", tipoContenido, contenido ?? ImagenValida, fechaCaptura);

    private void AssertNadaGuardado()
    {
        Assert.Empty(_almacenamiento.Archivos);
        Assert.Empty(_capturas.Capturas);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task Una_fecha_de_captura_futura_se_rechaza_sin_guardar_nada()
    {
        var (sesion, animal) = SesionYAnimal();
        var futura = Ahora + RegistrarCaptura.ToleranciaRelojCliente + TimeSpan.FromSeconds(1);

        var error = await Assert.ThrowsAsync<OperacionInvalidaError>(() =>
            CasoDeUso().EjecutarAsync(Comando(sesion, animal, fechaCaptura: futura), TestContext.Current.CancellationToken));

        Assert.Equal("La fecha de captura no puede ser posterior al momento actual", error.Message);
        AssertNadaGuardado();
    }

    [Fact]
    public async Task Una_fecha_de_captura_dentro_de_la_tolerancia_del_reloj_se_acepta()
    {
        var (sesion, animal) = SesionYAnimal();
        var adelantada = Ahora + RegistrarCaptura.ToleranciaRelojCliente;

        var captura = await CasoDeUso().EjecutarAsync(
            Comando(sesion, animal, fechaCaptura: adelantada), TestContext.Current.CancellationToken);

        Assert.Equal(adelantada, captura.FechaCaptura);
        Assert.Single(_capturas.Capturas);
    }

    [Fact]
    public async Task Iniciar_sesion_la_crea_activa_para_el_usuario_actual()
    {
        var caso = new IniciarSesionCaptura(_sesiones, _unidad, _usuario, _tiempo);

        var sesion = await caso.EjecutarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_usuario.IdUsuario, sesion.IdUsuario);
        Assert.Equal(Ahora, sesion.FechaInicio);
        Assert.Equal(EstadoSesionCaptura.Activa, sesion.Estado);
        Assert.Single(_sesiones.Sesiones);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Registrar_captura_valida_guarda_la_imagen_con_su_huella_en_una_transaccion()
    {
        var (sesion, animal) = SesionYAnimal();

        var respuesta = await CasoDeUso().EjecutarAsync(Comando(sesion, animal), TestContext.Current.CancellationToken);

        var archivo = Assert.Single(_almacenamiento.Archivos);
        Assert.Equal(ImagenValida, archivo.Value);
        var captura = Assert.Single(_capturas.Capturas);
        var imagen = Assert.Single(captura.Imagenes);
        Assert.Equal(archivo.Key, imagen.ReferenciaImagen);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(ImagenValida)), imagen.HuellaVerificacion);
        Assert.Equal("image/jpeg", imagen.TipoContenido);
        Assert.Equal(_usuario.IdUsuario, captura.IdUsuario);
        Assert.Equal(1, _unidad.Guardados);

        Assert.Equal(captura.Id, respuesta.Id);
        Assert.Equal(sesion.Id, respuesta.IdSesion);
        Assert.Equal(animal.Id, respuesta.IdAnimal);
        Assert.Equal(EstadoCaptura.Registrada, respuesta.Estado);
        Assert.Equal(Ahora, respuesta.FechaCaptura);
        Assert.Null(respuesta.IdEventoPesaje);
    }

    [Fact]
    public async Task La_fecha_de_captura_informada_se_respeta()
    {
        var (sesion, animal) = SesionYAnimal();
        var fecha = new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.FromHours(-3));

        var respuesta = await CasoDeUso().EjecutarAsync(
            Comando(sesion, animal, fechaCaptura: fecha), TestContext.Current.CancellationToken);

        Assert.Equal(fecha, respuesta.FechaCaptura);
    }

    [Theory]
    [InlineData("application/pdf", 10, "Formato de imagen no soportado")]
    [InlineData("image/webp", 10, "Formato de imagen no soportado")]
    [InlineData("image/png", 0, "El archivo está vacío")]
    [InlineData("image/jpeg", RegistrarCaptura.TamanioMaximoBytes + 1, "La imagen supera el tamaño máximo permitido")]
    public async Task Una_imagen_invalida_se_rechaza_sin_guardar_nada(string tipoContenido, int bytes, string motivo)
    {
        var (sesion, animal) = SesionYAnimal();

        var error = await Assert.ThrowsAsync<OperacionInvalidaError>(() => CasoDeUso().EjecutarAsync(
            Comando(sesion, animal, tipoContenido, new byte[bytes]), TestContext.Current.CancellationToken));

        Assert.Equal(motivo, error.Message);
        AssertNadaGuardado();
    }

    [Fact]
    public async Task Registrar_captura_con_sesion_inexistente_lanza_no_encontrado()
    {
        var (_, animal) = SesionYAnimal();
        var otraSesion = new SesionCaptura(_usuario.IdUsuario, Ahora);

        await Assert.ThrowsAsync<NoEncontradoError>(() => CasoDeUso().EjecutarAsync(
            Comando(otraSesion, animal), TestContext.Current.CancellationToken));
        AssertNadaGuardado();
    }

    [Fact]
    public async Task Registrar_captura_con_animal_inexistente_lanza_no_encontrado()
    {
        var (sesion, _) = SesionYAnimal();
        var animalDeOtroEstablecimiento = new Animal(Guid.CreateVersion7(), "AR999");

        await Assert.ThrowsAsync<NoEncontradoError>(() => CasoDeUso().EjecutarAsync(
            Comando(sesion, animalDeOtroEstablecimiento), TestContext.Current.CancellationToken));
        AssertNadaGuardado();
    }

    [Fact]
    public async Task Registrar_captura_en_una_sesion_finalizada_es_invalido()
    {
        var (sesion, animal) = SesionYAnimal();
        sesion.Finalizar(Ahora);

        await Assert.ThrowsAsync<OperacionInvalidaError>(() => CasoDeUso().EjecutarAsync(
            Comando(sesion, animal), TestContext.Current.CancellationToken));
        AssertNadaGuardado();
    }

    [Fact]
    public async Task Si_la_transaccion_falla_se_elimina_la_imagen_guardada()
    {
        var (sesion, animal) = SesionYAnimal();
        _unidad.FallarCon = new InvalidOperationException("base caída");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CasoDeUso().EjecutarAsync(
            Comando(sesion, animal), TestContext.Current.CancellationToken));

        Assert.Empty(_almacenamiento.Archivos);
    }

    [Fact]
    public async Task Finalizar_sesion_la_cierra_y_una_segunda_vez_es_invalido()
    {
        var (sesion, _) = SesionYAnimal();
        var caso = new FinalizarSesionCaptura(_sesiones, _unidad, _tiempo);

        var respuesta = await caso.EjecutarAsync(new FinalizarSesionCapturaComando(sesion.Id), TestContext.Current.CancellationToken);

        Assert.Equal(EstadoSesionCaptura.Finalizada, respuesta.Estado);
        Assert.Equal(Ahora, respuesta.FechaFin);
        Assert.Equal(1, _unidad.Guardados);
        await Assert.ThrowsAsync<OperacionInvalidaError>(() =>
            caso.EjecutarAsync(new FinalizarSesionCapturaComando(sesion.Id), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            caso.EjecutarAsync(new FinalizarSesionCapturaComando(Guid.CreateVersion7()), TestContext.Current.CancellationToken));
    }
}
