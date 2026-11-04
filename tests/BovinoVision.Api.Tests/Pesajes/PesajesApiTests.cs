using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BovinoVision.Api.Tests.Capturas;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Aplicacion.Analitica;
using BovinoVision.Infraestructura.Persistencia;
using BovinoVision.Dominio.Trazabilidad;
using Microsoft.EntityFrameworkCore;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Aplicacion.Pesajes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BovinoVision.Api.Tests.Pesajes;

public class PesajesApiTests(FabricaApi fabrica) : PruebaDeCapturas(fabrica)
{
    private readonly EstimadorControlado _estimador = new();
    private readonly Reloj _reloj = new();
    private readonly ControlAlertas _alertas = new();

    protected override void ConfigurarServicios(IServiceCollection servicios)
    {
        base.ConfigurarServicios(servicios);
        servicios.RemoveAll<IEstimadorPeso>();
        servicios.AddSingleton<IEstimadorPeso>(_estimador);
        servicios.RemoveAll<TimeProvider>();
        servicios.AddSingleton<TimeProvider>(_reloj);
        servicios.RemoveAll<IEvaluadorAlertas>();
        servicios.AddScoped<IEvaluadorAlertas>(p => new EvaluadorControlado(p.GetRequiredService<BovinoVisionDbContext>(), _alertas));
    }

    private async Task<bool> ProcesarAsync()
    {
        await using var alcance = FabricaCapturas.Services.CreateAsyncScope();
        return await alcance.ServiceProvider.GetRequiredService<ProcesarSiguienteCaptura>().EjecutarAsync(Cancelacion);
    }

    private async Task<CapturaRespuesta> CrearCapturaAsync()
    {
        var animal = await CrearAnimalAsync();
        var sesion = await IniciarSesionAsync();
        return await SubirCapturaValidaAsync(sesion.Id, animal.Id, _reloj.GetUtcNow().AddDays(-10));
    }

    private Task<CapturaRespuesta?> ConsultarAsync(Guid id) =>
        ClienteCapturas.GetFromJsonAsync<CapturaRespuesta>($"/capturas/{id}", Json.Opciones, Cancelacion);

    [Fact]
    public async Task Procesar_concurrentemente_crea_un_evento_fechado_en_la_captura_y_confirmar_no_lo_modifica()
    {
        var captura = await CrearCapturaAsync();
        var solicitudes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            ClienteCapturas.PostAsJsonAsync("/pesajes", new { id_captura = captura.Id }, Cancelacion)));
        Assert.Equal([HttpStatusCode.Accepted, HttpStatusCode.Conflict], solicitudes.Select(r => r.StatusCode).Order());
        foreach (var solicitud in solicitudes) { solicitud.Dispose(); }

        var procesados = await Task.WhenAll(ProcesarAsync(), ProcesarAsync());
        Assert.Single(procesados, p => p);
        Assert.Equal(1, _estimador.Llamadas);
        var final = (await ConsultarAsync(captura.Id))!;
        Assert.Equal(EstadoCaptura.Procesada, final.Estado);
        Assert.Equal(1, final.Intentos);
        Assert.NotNull(final.IdEventoPesaje);
        Assert.Null(final.MotivoFallo);
        var antes = await ClienteCapturas.GetStringAsync(new Uri($"/pesajes/{final.IdEventoPesaje}", UriKind.Relative), Cancelacion);
        var evento = System.Text.Json.JsonDocument.Parse(antes).RootElement;
        Assert.Equal(captura.FechaCaptura, evento.GetProperty("fecha_pesaje").GetDateTimeOffset());
        Assert.Equal(400m, evento.GetProperty("peso_kg").GetDecimal());
        Assert.Equal("fake-v1", evento.GetProperty("version_modelo").GetString());
        Assert.Equal("AR001", evento.GetProperty("animal_identificador").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, evento.GetProperty("ultimo_peso_previo").ValueKind);

        for (var i = 0; i < 2; i++)
        {
            using var revision = await ClienteCapturas.PostAsync(new Uri($"/capturas/{captura.Id}/confirmar", UriKind.Relative), null, Cancelacion);
            Assert.Equal(HttpStatusCode.OK, revision.StatusCode);
        }
        Assert.NotNull((await ConsultarAsync(captura.Id))!.FechaRevision);
        Assert.Equal(antes, await ClienteCapturas.GetStringAsync(new Uri($"/pesajes/{final.IdEventoPesaje}", UriKind.Relative), Cancelacion));
        Assert.False(await ProcesarAsync());
        using var duplicado = await ClienteCapturas.PostAsJsonAsync("/pesajes", new { id_captura = captura.Id }, Cancelacion);
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
        Assert.Equal(final.IdEventoPesaje, (await ConsultarAsync(captura.Id))!.IdEventoPesaje);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Un_resultado_invalido_o_agotar_reintentos_falla_sin_evento(bool indisponible)
    {
        var captura = await CrearCapturaAsync();
        _estimador.Indisponible = indisponible;
        _estimador.Resultado = new(null, "fake-v1", "No se detectó el animal");
        using var solicitud = await ClienteCapturas.PostAsJsonAsync("/pesajes", new { id_captura = captura.Id }, Cancelacion);
        Assert.Equal(HttpStatusCode.Accepted, solicitud.StatusCode);
        var maximo = indisponible ? 3 : 1;
        for (var i = 1; i <= maximo; i++)
        {
            Assert.True(await ProcesarAsync());
            var actual = (await ConsultarAsync(captura.Id))!;
            Assert.Equal(i, actual.Intentos);
            Assert.Null(actual.IdEventoPesaje);
            Assert.Equal(i == maximo ? EstadoCaptura.Fallida : EstadoCaptura.Pendiente, actual.Estado);
            Assert.Equal(indisponible ? "El servicio de estimación no está disponible" : "No se detectó el animal", actual.MotivoFallo);
            Assert.False(await ProcesarAsync());
            _reloj.Ahora = _reloj.Ahora.AddSeconds(31);
        }
        Assert.Equal(maximo, _estimador.Llamadas);
        Assert.False(await ProcesarAsync());
        using var revision = await ClienteCapturas.PostAsync(new Uri($"/capturas/{captura.Id}/confirmar", UriKind.Relative), null, Cancelacion);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, revision.StatusCode);
        Assert.Null((await ConsultarAsync(captura.Id))!.FechaRevision);
    }

    [Fact]
    public async Task Un_fallo_reintentable_informado_por_el_estimador_se_reintenta_con_su_motivo()
    {
        var captura = await CrearCapturaAsync();
        _estimador.Resultado = new(null, "", "El modelo de estimación no está disponible", Reintentable: true);
        using var solicitud = await ClienteCapturas.PostAsJsonAsync("/pesajes", new { id_captura = captura.Id }, Cancelacion);
        Assert.Equal(HttpStatusCode.Accepted, solicitud.StatusCode);

        Assert.True(await ProcesarAsync());
        var reintento = (await ConsultarAsync(captura.Id))!;
        Assert.Equal(EstadoCaptura.Pendiente, reintento.Estado);
        Assert.Equal("El modelo de estimación no está disponible", reintento.MotivoFallo);

        _estimador.Resultado = new(380.5m, "fake-v1");
        _reloj.Ahora = _reloj.Ahora.AddSeconds(31);
        Assert.True(await ProcesarAsync());
        var final = (await ConsultarAsync(captura.Id))!;
        Assert.Equal(EstadoCaptura.Procesada, final.Estado);
        Assert.Equal(2, final.Intentos);
        Assert.NotNull(final.IdEventoPesaje);
        Assert.Equal([1, 2], _estimador.Intentos);
    }

    [Fact]
    public async Task Reclamo_vencido_se_recupera_y_el_trabajador_anterior_no_puede_publicar()
    {
        var captura = await CrearCapturaAsync();
        using var solicitud = await ClienteCapturas.PostAsJsonAsync("/pesajes", new { id_captura = captura.Id }, Cancelacion);
        Assert.Equal(HttpStatusCode.Accepted, solicitud.StatusCode);
        var empezo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continuar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _estimador.Esperar = async () => { empezo.SetResult(); await continuar.Task; };
        var viejo = ProcesarAsync();
        await empezo.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancelacion);
        Assert.Equal(EstadoCaptura.Procesando, (await ConsultarAsync(captura.Id))!.Estado);
        Assert.False(await ProcesarAsync());
        _reloj.Ahora = _reloj.Ahora.AddSeconds(121);
        _estimador.Esperar = null;
        Assert.True(await ProcesarAsync());
        var vigente = (await ConsultarAsync(captura.Id))!;
        continuar.SetResult();
        Assert.True(await viejo);
        Assert.Equal(vigente, await ConsultarAsync(captura.Id));
        Assert.Equal(EstadoCaptura.Procesada, vigente.Estado);
        Assert.Equal(2, vigente.Intentos);
        Assert.NotNull(vigente.IdEventoPesaje);
    }

    [Fact]
    public async Task La_evaluacion_de_alertas_y_el_evento_se_confirman_juntos_y_un_fallo_no_publica_nada()
    {
        var captura = await CrearCapturaAsync();
        using var solicitud = await ClienteCapturas.PostAsJsonAsync("/pesajes", new { id_captura = captura.Id }, Cancelacion);
        Assert.Equal(HttpStatusCode.Accepted, solicitud.StatusCode);
        _alertas.Fallar = true;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ProcesarAsync());
        Assert.Equal("Fallo de evaluación simulado", error.Message);
        var intermedia = (await ConsultarAsync(captura.Id))!;
        Assert.Equal(EstadoCaptura.Procesando, intermedia.Estado);
        Assert.Null(intermedia.IdEventoPesaje);
        Assert.Equal(1, intermedia.Intentos);
        _alertas.Fallar = false;
        _reloj.Ahora = _reloj.Ahora.AddSeconds(121);
        Assert.True(await ProcesarAsync());
        var final = (await ConsultarAsync(captura.Id))!;
        var evento = await ClienteCapturas.GetFromJsonAsync<PesajeRespuesta>($"/pesajes/{final.IdEventoPesaje}", Json.Opciones, Cancelacion);
        Assert.Equal(EstadoCaptura.Procesada, final.Estado);
        Assert.Equal(2, final.Intentos);
        Assert.NotNull(evento!.Alerta);
        Assert.Equal(evento.Id, evento.Alerta.IdEvento);
        Assert.Equal(2, _alertas.Llamadas);
    }

    [Fact]
    public async Task Los_reclamos_abandonados_agotan_el_limite_sin_evento()
    {
        var captura = await CrearCapturaAsync();
        using var solicitud = await ClienteCapturas.PostAsJsonAsync("/pesajes", new { id_captura = captura.Id }, Cancelacion);
        Assert.Equal(HttpStatusCode.Accepted, solicitud.StatusCode);
        for (var i = 1; i <= 3; i++)
        {
            await using var alcance = FabricaCapturas.Services.CreateAsyncScope();
            var cola = alcance.ServiceProvider.GetRequiredService<IColaInferencia>();
            var trabajo = await cola.ReclamarAsync(_reloj.Ahora, TimeSpan.FromSeconds(120), 3, Cancelacion);
            Assert.Equal(i, trabajo!.Intento);
            Assert.Equal(EstadoCaptura.Procesando, (await ConsultarAsync(captura.Id))!.Estado);
            _reloj.Ahora = _reloj.Ahora.AddSeconds(121);
        }
        Assert.False(await ProcesarAsync());
        var final = (await ConsultarAsync(captura.Id))!;
        Assert.Equal(EstadoCaptura.Fallida, final.Estado);
        Assert.Equal(3, final.Intentos);
        Assert.Equal("Se agotaron los intentos de estimación tras vencer el procesamiento", final.MotivoFallo);
        Assert.Null(final.IdEventoPesaje);
        Assert.Equal(0, _estimador.Llamadas);
        Assert.Equal(0, _alertas.Llamadas);
    }

    [Fact]
    public async Task Un_reintento_exitoso_limpia_el_motivo_y_la_cola_funciona_en_segundo_plano()
    {
        var captura = await CrearCapturaAsync();
        using var solicitud = await ClienteCapturas.PostAsJsonAsync("/pesajes", new { id_captura = captura.Id }, Cancelacion);
        Assert.Equal(HttpStatusCode.Accepted, solicitud.StatusCode);
        _estimador.Indisponible = true;
        Assert.True(await ProcesarAsync());
        Assert.Equal(EstadoCaptura.Pendiente, (await ConsultarAsync(captura.Id))!.Estado);
        _estimador.Indisponible = false;
        _reloj.Ahora = _reloj.Ahora.AddSeconds(31);
        FabricaCapturas.Services.GetRequiredService<IOptions<OpcionesInferencia>>().Value.ProcesarEnSegundoPlano = true;
        CapturaRespuesta? final = null;
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(Cancelacion);
        limite.CancelAfter(TimeSpan.FromSeconds(15));
        do
        {
            await Task.Delay(50, limite.Token);
            final = await ConsultarAsync(captura.Id);
        } while (final!.Estado != EstadoCaptura.Procesada);
        Assert.Equal(2, final.Intentos);
        Assert.NotNull(final.IdEventoPesaje);
        Assert.Null(final.MotivoFallo);
        Assert.Equal(2, _estimador.Llamadas);
    }

    [Fact]
    public async Task El_resultado_incluye_el_lote_historico_y_el_peso_previo_por_fecha_de_captura()
    {
        var animal = await CrearAnimalAsync();
        var fecha = _reloj.Ahora.AddDays(-10);
        await EnContextoAsync(async contexto =>
        {
            var lote = new Lote(DatosSemilla.IdEstablecimiento, "L1");
            var actual = new Lote(DatosSemilla.IdEstablecimiento, "L2");
            contexto.Lotes.AddRange(lote, actual);
            var entidad = await contexto.Animales.Include(a => a.HistorialLotes).SingleAsync(a => a.Id == animal.Id, Cancelacion);
            entidad.AsignarLote(lote.Id, DatosSemilla.IdUsuarioSistema, fecha.AddDays(-1));
            entidad.AsignarLote(actual.Id, DatosSemilla.IdUsuarioSistema, fecha.AddDays(2));
            // Los UUID los asigna el dominio; se preparan explícitamente las asignaciones recién creadas.
            contexto.HistorialLotes.AddRange(entidad.HistorialLotes);
            await contexto.SaveChangesAsync(Cancelacion);
            return true;
        });
        var sesion = await IniciarSesionAsync();
        foreach (var dia in new[] { 0, 1 })
        {
            var captura = await SubirCapturaValidaAsync(sesion.Id, animal.Id, fecha.AddDays(dia));
            using var solicitud = await ClienteCapturas.PostAsJsonAsync("/pesajes", new { id_captura = captura.Id }, Cancelacion);
            Assert.Equal(HttpStatusCode.Accepted, solicitud.StatusCode);
            Assert.True(await ProcesarAsync());
            var final = (await ConsultarAsync(captura.Id))!;
            var evento = await ClienteCapturas.GetFromJsonAsync<PesajeRespuesta>($"/pesajes/{final.IdEventoPesaje}", Json.Opciones, Cancelacion);
            Assert.Equal("L1", evento!.LoteCodigo);
            Assert.Equal(fecha.AddDays(dia), evento.FechaPesaje);
            if (dia == 0) { Assert.Null(evento.UltimoPesoPrevio); }
            else { Assert.Equal(new PesoPrevioRespuesta(400m, fecha), evento.UltimoPesoPrevio); }
        }
    }

    [Fact]
    public async Task El_pesaje_expone_el_id_de_la_imagen_utilizada()
    {
        var captura = await CrearCapturaAsync();
        using var solicitud = await ClienteCapturas.PostAsJsonAsync("/pesajes", new { id_captura = captura.Id }, Cancelacion);
        Assert.Equal(HttpStatusCode.Accepted, solicitud.StatusCode);
        Assert.True(await ProcesarAsync());
        var final = (await ConsultarAsync(captura.Id))!;

        var pesaje = await ClienteCapturas.GetFromJsonAsync<JsonElement>($"/pesajes/{final.IdEventoPesaje}", Json.Opciones, Cancelacion);

        var imagen = await EnContextoAsync(contexto => contexto.Imagenes.SingleAsync(i => i.IdCaptura == captura.Id, Cancelacion));
        var evento = await EnContextoAsync(contexto => contexto.EventosPesaje.SingleAsync(e => e.Id == final.IdEventoPesaje, Cancelacion));
        Assert.Equal(evento.IdImagenUtilizada, imagen.Id);
        Assert.Equal(imagen.Id, pesaje.GetProperty("id_imagen").GetGuid());
        Assert.Equal(captura.Id, pesaje.GetProperty("id_captura").GetGuid());
        Assert.Equal("estimado", pesaje.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Las_referencias_inexistentes_devuelven_404()
    {
        var id = Guid.CreateVersion7();
        using var solicitar = await ClienteCapturas.PostAsJsonAsync("/pesajes", new { id_captura = id }, Cancelacion);
        using var confirmar = await ClienteCapturas.PostAsync(new Uri($"/capturas/{id}/confirmar", UriKind.Relative), null, Cancelacion);
        using var obtener = await ClienteCapturas.GetAsync(new Uri($"/pesajes/{id}", UriKind.Relative), Cancelacion);
        Assert.Equal(HttpStatusCode.NotFound, solicitar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, confirmar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, obtener.StatusCode);
        Assert.Equal((0, 0), await ContarFilasAsync());
        Assert.Empty(Almacenamiento.Archivos);
    }

    [Fact]
    public async Task Solicitar_pesaje_responde_202_y_permite_consultar_la_cola()
    {
        var animal = await CrearAnimalAsync();
        var sesion = await IniciarSesionAsync();
        var captura = await SubirCapturaValidaAsync(sesion.Id, animal.Id);

        using var respuesta = await ClienteCapturas.PostAsJsonAsync("/pesajes", new { id_captura = captura.Id }, Cancelacion);

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        Assert.Equal($"/capturas/{captura.Id}", respuesta.Headers.Location!.OriginalString);
        var pendiente = await ClienteCapturas.GetFromJsonAsync<CapturaRespuesta>($"/capturas/{captura.Id}", Json.Opciones, Cancelacion);
        Assert.Equal(EstadoCaptura.Pendiente, pendiente!.Estado);
        Assert.Equal(0, pendiente.Intentos);
        Assert.Null(pendiente.IdEventoPesaje);
        Assert.Null(pendiente.MotivoFallo);
    }

    [Fact]
    public async Task Revisar_una_captura_sin_resultado_responde_422_sin_alterarla()
    {
        var animal = await CrearAnimalAsync();
        var sesion = await IniciarSesionAsync();
        var captura = await SubirCapturaValidaAsync(sesion.Id, animal.Id);

        using var respuesta = await ClienteCapturas.PostAsync(new Uri($"/capturas/{captura.Id}/confirmar", UriKind.Relative), null, Cancelacion);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        var actual = await ClienteCapturas.GetFromJsonAsync<CapturaRespuesta>($"/capturas/{captura.Id}", Json.Opciones, Cancelacion);
        Assert.Equal(EstadoCaptura.Registrada, actual!.Estado);
        Assert.Equal(0, actual.Intentos);
        Assert.Null(actual.FechaRevision);
        Assert.Null(actual.IdEventoPesaje);
    }
}
