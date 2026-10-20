using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Pesajes;

namespace BovinoVision.Dominio.Tests.Capturas;

public class CapturaTests
{
    private static readonly DateTimeOffset FechaCaptura = new(2026, 10, 1, 9, 30, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset Ahora = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private static Captura NuevaCaptura() =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), FechaCaptura);

    private static Captura CapturaProcesando()
    {
        var captura = NuevaCaptura();
        captura.AgregarImagen("ref.jpg", "hash", "image/jpeg");
        captura.SolicitarEstimacion();
        captura.IniciarProcesamiento();
        return captura;
    }

    [Fact]
    public void Una_captura_nueva_queda_registrada_con_la_fecha_en_utc()
    {
        var captura = NuevaCaptura();

        Assert.Equal(EstadoCaptura.Registrada, captura.Estado);
        Assert.Equal(TimeSpan.Zero, captura.FechaCaptura.Offset);
        Assert.Equal(FechaCaptura, captura.FechaCaptura);
    }

    [Fact]
    public void Solicitar_estimacion_sin_imagenes_es_invalido()
    {
        Assert.Throws<OperacionInvalidaError>(() => NuevaCaptura().SolicitarEstimacion());
    }

    [Fact]
    public void Solicitar_estimacion_dos_veces_es_un_conflicto()
    {
        var captura = NuevaCaptura();
        captura.AgregarImagen("ref.jpg", "hash", "image/jpeg");
        captura.SolicitarEstimacion();

        Assert.Throws<ConflictoError>(captura.SolicitarEstimacion);
    }

    [Fact]
    public void Procesar_incrementa_intentos_y_finaliza_procesada()
    {
        var captura = CapturaProcesando();

        captura.MarcarProcesada();

        Assert.Equal(EstadoCaptura.Procesada, captura.Estado);
        Assert.Equal(1, captura.Intentos);
        Assert.Throws<ConflictoError>(captura.SolicitarEstimacion);
    }

    [Fact]
    public void Una_estimacion_fallida_guarda_el_motivo()
    {
        var captura = CapturaProcesando();

        captura.MarcarFallida("Animal no detectable");

        Assert.Equal(EstadoCaptura.Fallida, captura.Estado);
        Assert.Equal("Animal no detectable", captura.MotivoFallo);
        Assert.Throws<OperacionInvalidaError>(captura.SolicitarEstimacion);
    }

    [Fact]
    public void Reintentar_despues_vuelve_a_pendiente_con_fecha_de_proximo_intento()
    {
        var captura = CapturaProcesando();

        captura.ReintentarDespues("Servicio no disponible", Ahora);

        Assert.Equal(EstadoCaptura.Pendiente, captura.Estado);
        Assert.Equal(Ahora, captura.ProximoIntento);
        captura.IniciarProcesamiento();
        Assert.Equal(2, captura.Intentos);
        Assert.Null(captura.ProximoIntento);
    }

    [Fact]
    public void El_vencimiento_del_procesamiento_se_guarda_aparte_de_la_fecha_de_reintento()
    {
        var captura = CapturaProcesando();
        captura.ReintentarDespues("Servicio no disponible", Ahora);
        var vencimiento = Ahora.AddMinutes(2).ToOffset(TimeSpan.FromHours(-3));

        captura.IniciarProcesamiento(vencimiento);

        Assert.Equal(vencimiento, captura.VenceProcesamiento);
        Assert.Equal(TimeSpan.Zero, captura.VenceProcesamiento!.Value.Offset);
        Assert.Null(captura.ProximoIntento);

        captura.ReintentarDespues("Servicio no disponible", Ahora.AddMinutes(5));
        Assert.Null(captura.VenceProcesamiento);
        Assert.Equal(Ahora.AddMinutes(5), captura.ProximoIntento);

        captura.IniciarProcesamiento(vencimiento);
        captura.MarcarProcesada();
        Assert.Null(captura.VenceProcesamiento);
        Assert.Null(captura.ProximoIntento);
    }

    [Fact]
    public void Una_captura_fallida_no_conserva_vencimiento_de_procesamiento()
    {
        var captura = NuevaCaptura();
        captura.AgregarImagen("ref.jpg", "hash", "image/jpeg");
        captura.SolicitarEstimacion();
        captura.IniciarProcesamiento(Ahora);

        captura.MarcarFallida("Animal no detectable");

        Assert.Null(captura.VenceProcesamiento);
    }

    [Fact]
    public void La_revision_solo_se_registra_sobre_una_captura_procesada_y_no_se_pisa()
    {
        var captura = CapturaProcesando();
        Assert.Throws<OperacionInvalidaError>(() => captura.RegistrarRevision(Ahora));

        captura.MarcarProcesada();
        captura.RegistrarRevision(Ahora);
        captura.RegistrarRevision(Ahora.AddDays(1));

        Assert.Equal(Ahora, captura.FechaRevision);
    }

    [Fact]
    public void El_evento_de_pesaje_toma_la_fecha_de_la_captura_y_no_la_del_procesamiento()
    {
        var captura = CapturaProcesando();
        var imagen = Assert.Single(captura.Imagenes);

        var evento = EventoPesaje.Registrar(captura, imagen.Id, null, 350.5m, Ahora, "mock-v1");

        Assert.Equal(captura.FechaCaptura, evento.FechaPesaje);
        Assert.Equal(Ahora, evento.FechaProcesamiento);
        Assert.Equal(captura.IdAnimal, evento.IdAnimal);
        Assert.Equal(captura.Id, evento.IdCaptura);
    }

    [Fact]
    public void El_evento_de_pesaje_rechaza_peso_no_positivo_e_imagen_ajena()
    {
        var captura = CapturaProcesando();
        var imagen = Assert.Single(captura.Imagenes);

        Assert.Throws<OperacionInvalidaError>(() => EventoPesaje.Registrar(captura, imagen.Id, null, 0m, Ahora, "v"));
        Assert.Throws<OperacionInvalidaError>(() => EventoPesaje.Registrar(captura, Guid.CreateVersion7(), null, 300m, Ahora, "v"));
    }
}
