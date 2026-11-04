using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Trazabilidad;

namespace BovinoVision.Aplicacion.Tests.Capturas;

public class DescartarCapturaTests
{
    private const string Referencia = "memoria/foto.jpg";

    private readonly CapturasEnMemoria _capturas = new();
    private readonly AnimalesEnMemoria _animales = new();
    private readonly AlmacenamientoEnMemoria _almacenamiento = new();
    private readonly UnidadDeTrabajoContadora _unidad = new();
    private readonly ContextoUsuarioFijo _usuario = new();

    private DescartarCaptura CasoDeUso() =>
        new(new ColaInferenciaEnMemoria(_capturas), _capturas, _animales, _almacenamiento, _unidad, _usuario);

    private Captura CapturaRegistrada(Guid? idEstablecimiento = null)
    {
        var animal = new Animal(idEstablecimiento ?? _usuario.IdEstablecimiento, "AR001");
        _animales.Animales.Add(animal);
        var captura = new Captura(animal.Id, _usuario.IdUsuario, Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        captura.AgregarImagen(Referencia, "hash", "image/jpeg");
        _capturas.Agregar(captura);
        _almacenamiento.Archivos[Referencia] = [1, 2, 3];
        return captura;
    }

    [Fact]
    public async Task Descartar_una_captura_registrada_la_elimina_junto_con_su_archivo()
    {
        var captura = CapturaRegistrada();

        await CasoDeUso().EjecutarAsync(new DescartarCapturaComando(captura.Id), TestContext.Current.CancellationToken);

        Assert.Empty(_capturas.Capturas);
        Assert.Empty(_almacenamiento.Archivos);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Una_captura_que_ya_no_esta_registrada_se_rechaza_sin_borrar_nada()
    {
        var captura = CapturaRegistrada();
        captura.SolicitarEstimacion();

        await Assert.ThrowsAsync<OperacionInvalidaError>(() =>
            CasoDeUso().EjecutarAsync(new DescartarCapturaComando(captura.Id), TestContext.Current.CancellationToken));

        Assert.Single(_capturas.Capturas);
        Assert.Single(_almacenamiento.Archivos);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task Una_captura_inexistente_o_de_otro_establecimiento_es_no_encontrada()
    {
        var ajena = CapturaRegistrada(Guid.CreateVersion7());

        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            CasoDeUso().EjecutarAsync(new DescartarCapturaComando(Guid.CreateVersion7()), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            CasoDeUso().EjecutarAsync(new DescartarCapturaComando(ajena.Id), TestContext.Current.CancellationToken));

        Assert.Single(_capturas.Capturas);
        Assert.Single(_almacenamiento.Archivos);
        Assert.Equal(0, _unidad.Guardados);
    }

    [Fact]
    public async Task Si_no_se_pudo_guardar_el_cambio_el_archivo_se_conserva()
    {
        var captura = CapturaRegistrada();
        _unidad.FallarCon = new InvalidOperationException("db caída");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CasoDeUso().EjecutarAsync(new DescartarCapturaComando(captura.Id), TestContext.Current.CancellationToken));

        Assert.Single(_almacenamiento.Archivos);
    }
}
