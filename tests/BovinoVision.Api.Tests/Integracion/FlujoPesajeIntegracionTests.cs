using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Api.Tests.Pesajes;
using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Aplicacion.Pesajes;
using BovinoVision.Dominio.Capturas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BovinoVision.Api.Tests.Integracion;

public sealed class FlujoPesajeIntegracionTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    private readonly EstimadorControlado _estimador = new();
    private readonly Reloj _reloj = new();
    private readonly DirectoryInfo _imagenes = Directory.CreateTempSubdirectory("bovino-integracion-");
    private WebApplicationFactory<Program>? _host;
    private HttpClient? _cliente;
    private HttpClient Api => _cliente ?? throw new InvalidOperationException("Host no iniciado");
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private void IniciarHost(bool procesar)
    {
        _host = Fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Inferencia:ProcesarEnSegundoPlano", procesar.ToString());
            builder.UseSetting("Almacenamiento:DirectorioImagenes", _imagenes.FullName);
            builder.ConfigureTestServices(servicios =>
            {
                servicios.RemoveAll<IEstimadorPeso>();
                servicios.AddSingleton<IEstimadorPeso>(_estimador);
                servicios.RemoveAll<TimeProvider>();
                servicios.AddSingleton<TimeProvider>(_reloj);
            });
        });
        _cliente = _host.CreateClient();
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            _cliente?.Dispose();
            if (_host is not null) { await _host.DisposeAsync(); }
        }
        finally
        {
            _imagenes.Delete(recursive: true);
            await base.DisposeAsync();
        }
    }

    [Fact]
    public async Task El_arranque_con_consumidor_activo_migra_y_precarga_antes_de_consultar_la_cola()
    {
        await using var fabrica = new FabricaApi(procesarEnSegundoPlano: true);
        await fabrica.InitializeAsync();
        using var cliente = fabrica.CreateClient();
        using var salud = await cliente.GetAsync(new Uri("/health", UriKind.Relative), Cancelacion);
        Assert.Equal(HttpStatusCode.OK, salud.StatusCode);
        Assert.Equal("{\"status\":\"ok\"}", await salud.Content.ReadAsStringAsync(Cancelacion));
        using var sesion = await cliente.PostAsync(new Uri("/sesiones-captura", UriKind.Relative), null, Cancelacion);
        Assert.Equal(HttpStatusCode.Created, sesion.StatusCode);
        Assert.NotEqual(Guid.Empty, (await sesion.LeerAsync<JsonElement>()).GetProperty("id_usuario").GetGuid());
        await Task.Delay(TimeSpan.FromSeconds(3), Cancelacion);
        Assert.Empty(fabrica.ErroresInferencia);
    }

    [Fact]
    public async Task Un_host_sin_consumidor_no_es_procesado_por_la_fabrica_compartida()
    {
        IniciarHost(procesar: false);
        var (animal, sesion, _) = await PrepararAsync();
        var captura = await SubirAsync(animal, sesion, _reloj.Ahora);
        await SolicitarAsync(captura.Id);

        // Observe multiple default worker intervals, not just the instant after enqueueing.
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(Cancelacion);
        limite.CancelAfter(TimeSpan.FromSeconds(10));
        var observacion = Task.Delay(TimeSpan.FromSeconds(3), limite.Token);
        do
        {
            var actual = await ConsultarAsync(captura.Id, limite.Token);
            Assert.Equal(EstadoCaptura.Pendiente, actual.Estado);
            Assert.Equal(0, actual.Intentos);
            Assert.Null(actual.IdEventoPesaje);
            await Task.Delay(50, limite.Token);
        } while (!observacion.IsCompleted);
        Assert.Equal(0, _estimador.Llamadas);
        Assert.Empty((await ObtenerAsync($"/animales/{animal}/pesajes")).EnumerateArray());
        Assert.Empty((await ObtenerAsync("/alertas")).EnumerateArray());
    }

    [Theory]
    [InlineData(390, "perdida_peso", -1, 1)]
    [InlineData(402, "estancamiento", 0.2, 2)]
    public async Task El_consumidor_real_publica_pesajes_alertas_y_analitica_sin_duplicar_ni_mutar(
        int segundoPeso, string tipoAlerta, decimal gmd, int prioridad)
    {
        IniciarHost(procesar: true);
        var (animal, sesion, lote) = await PrepararAsync();
        var fechaInicial = _reloj.Ahora;
        var primera = await SubirAsync(animal, sesion, fechaInicial);
        await SolicitarAsync(primera.Id);
        var procesada = await EsperarFinalAsync(primera.Id);
        Assert.Equal(EstadoCaptura.Procesada, procesada.Estado);
        Assert.NotNull(procesada.IdEventoPesaje);
        Assert.Empty((await ObtenerAsync("/alertas")).EnumerateArray());

        _estimador.Resultado = new(segundoPeso, "fake-v1");
        _reloj.Ahora = fechaInicial.AddDays(12);
        var fechaSegunda = fechaInicial.AddDays(10);
        var segunda = await SubirAsync(animal, sesion, fechaSegunda);
        await SolicitarAsync(segunda.Id);
        var final = await EsperarFinalAsync(segunda.Id);
        Assert.Equal(EstadoCaptura.Procesada, final.Estado);
        Assert.Equal(1, final.Intentos);
        Assert.Null(final.MotivoFallo);
        Assert.Null(final.FechaRevision);
        Assert.NotNull(final.IdEventoPesaje);

        var eventoAntes = await ObtenerAsync($"/pesajes/{final.IdEventoPesaje}");
        Assert.Equal(fechaSegunda, eventoAntes.GetProperty("fecha_pesaje").GetDateTimeOffset());
        Assert.Equal(segundoPeso, eventoAntes.GetProperty("peso_kg").GetDecimal());
        Assert.Equal("E2E", eventoAntes.GetProperty("lote_codigo").GetString());
        Assert.Equal(400m, eventoAntes.GetProperty("ultimo_peso_previo").GetProperty("peso_kg").GetDecimal());
        var historialAntes = await ObtenerAsync($"/animales/{animal}/pesajes");
        Assert.Equal(2, historialAntes.GetArrayLength());
        Assert.Equal(final.IdEventoPesaje, historialAntes[0].GetProperty("id").GetGuid());
        Assert.Equal(gmd, historialAntes[0].GetProperty("gmd_kg_dia").GetDecimal());
        Assert.Equal(_reloj.Ahora, historialAntes[0].GetProperty("fecha_procesamiento").GetDateTimeOffset());
        Assert.Equal(procesada.IdEventoPesaje, historialAntes[1].GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, historialAntes[1].GetProperty("gmd_kg_dia").ValueKind);

        var alertasAntes = await ObtenerAsync("/alertas");
        var alerta = Assert.Single(alertasAntes.EnumerateArray());
        Assert.Equal(final.IdEventoPesaje, alerta.GetProperty("id_evento").GetGuid());
        Assert.Equal(animal, alerta.GetProperty("id_animal").GetGuid());
        Assert.Equal(tipoAlerta, alerta.GetProperty("tipo").GetString());
        Assert.Equal(gmd, alerta.GetProperty("variacion").GetDecimal());
        Assert.Equal(prioridad, alerta.GetProperty("prioridad").GetInt32());
        Assert.Equal(alertasAntes.GetRawText(), (await ObtenerAsync($"/animales/{animal}/alertas")).GetRawText());
        var panelAntes = await ObtenerAsync($"/panel?id_lote={lote}");
        Assert.Equal(1, panelAntes.GetProperty("cantidad_animales").GetInt32());
        Assert.Equal(segundoPeso, panelAntes.GetProperty("peso_promedio_kg").GetDecimal());
        Assert.Equal(gmd, panelAntes.GetProperty("gmd_promedio_kg_dia").GetDecimal());
        var rutaReporte = $"/reportes?desde=2026-10-20&hasta=2026-10-30&id_lote={lote}";
        var reporteAntes = await ObtenerAsync(rutaReporte);
        Assert.Equal(2, reporteAntes.GetProperty("cantidad_pesajes").GetInt32());
        Assert.Equal((400m + segundoPeso) / 2, reporteAntes.GetProperty("peso_promedio_kg").GetDecimal());
        Assert.Equal(gmd, reporteAntes.GetProperty("gmd_promedio_kg_dia").GetDecimal());

        using var duplicado = await Api.PostAsJsonAsync("/pesajes", new { id_captura = segunda.Id }, Cancelacion);
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
        Assert.Equal(final, await ConsultarAsync(segunda.Id, Cancelacion));
        for (var i = 0; i < 2; i++)
        {
            using var confirmar = await Api.PostAsync(new Uri($"/capturas/{segunda.Id}/confirmar", UriKind.Relative), null, Cancelacion);
            Assert.Equal(HttpStatusCode.OK, confirmar.StatusCode);
        }
        var revisada = await ConsultarAsync(segunda.Id, Cancelacion);
        Assert.Equal(final with { FechaRevision = _reloj.Ahora }, revisada);

        _estimador.Resultado = new(null, "fake-v1", "No se detectó el animal");
        var fallida = await SubirAsync(animal, sesion, _reloj.Ahora);
        await SolicitarAsync(fallida.Id);
        var fallo = await EsperarFinalAsync(fallida.Id);
        Assert.Equal(EstadoCaptura.Fallida, fallo.Estado);
        Assert.Equal("No se detectó el animal", fallo.MotivoFallo);
        Assert.Equal(1, fallo.Intentos);
        Assert.Null(fallo.IdEventoPesaje);
        Assert.Null(fallo.FechaRevision);
        using var rechazada = await Api.PostAsync(new Uri($"/capturas/{fallida.Id}/confirmar", UriKind.Relative), null, Cancelacion);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rechazada.StatusCode);
        Assert.Equal(fallo, await ConsultarAsync(fallida.Id, Cancelacion));
        Assert.Equal(3, _estimador.Llamadas);
        Assert.Equal(eventoAntes.GetRawText(), (await ObtenerAsync($"/pesajes/{final.IdEventoPesaje}")).GetRawText());
        Assert.Equal(historialAntes.GetRawText(), (await ObtenerAsync($"/animales/{animal}/pesajes")).GetRawText());
        Assert.Equal(alertasAntes.GetRawText(), (await ObtenerAsync("/alertas")).GetRawText());
        Assert.Equal(panelAntes.GetRawText(), (await ObtenerAsync($"/panel?id_lote={lote}")).GetRawText());
        Assert.Equal(reporteAntes.GetRawText(), (await ObtenerAsync(rutaReporte)).GetRawText());
    }

    private async Task<(Guid Animal, Guid Sesion, Guid Lote)> PrepararAsync()
    {
        using var lote = await Api.PostAsJsonAsync("/lotes", new { codigo = "E2E", gmd_objetivo = 1m }, Cancelacion);
        Assert.Equal(HttpStatusCode.Created, lote.StatusCode);
        var idLote = (await lote.LeerAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var animal = await Api.PostAsJsonAsync("/animales", new { identificador = "E2E-01", id_lote = idLote }, Cancelacion);
        Assert.Equal(HttpStatusCode.Created, animal.StatusCode);
        var cuerpo = await animal.LeerAsync<JsonElement>();
        Assert.Equal(idLote, cuerpo.GetProperty("id_lote").GetGuid());
        Assert.NotEqual(Guid.Empty, cuerpo.GetProperty("id_establecimiento").GetGuid());
        using var sesion = await Api.PostAsync(new Uri("/sesiones-captura", UriKind.Relative), null, Cancelacion);
        Assert.Equal(HttpStatusCode.Created, sesion.StatusCode);
        var datos = await sesion.LeerAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, datos.GetProperty("id_usuario").GetGuid());
        return (cuerpo.GetProperty("id").GetGuid(), datos.GetProperty("id").GetGuid(), idLote);
    }

    private async Task<CapturaRespuesta> SubirAsync(Guid animal, Guid sesion, DateTimeOffset fecha)
    {
        using var formulario = new MultipartFormDataContent
        {
            { new StringContent(animal.ToString()), "id_animal" },
            { new StringContent(sesion.ToString()), "id_sesion" },
            { new StringContent(fecha.ToString("O", CultureInfo.InvariantCulture)), "fecha_captura" },
        };
        var archivo = new ByteArrayContent("foto-e2e"u8.ToArray());
        archivo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        formulario.Add(archivo, "imagen", "foto.jpg");
        using var respuesta = await Api.PostAsync(new Uri("/capturas", UriKind.Relative), formulario, Cancelacion);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.LeerAsync<CapturaRespuesta>())!;
    }

    private async Task SolicitarAsync(Guid id)
    {
        using var respuesta = await Api.PostAsJsonAsync("/pesajes", new { id_captura = id }, Cancelacion);
        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        Assert.Equal($"/capturas/{id}", respuesta.Headers.Location!.OriginalString);
        Assert.Empty(await respuesta.Content.ReadAsStringAsync(Cancelacion));
    }

    private async Task<JsonElement> ObtenerAsync(string ruta)
    {
        using var respuesta = await Api.GetAsync(new Uri(ruta, UriKind.Relative), Cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.LeerAsync<JsonElement>();
    }

    private async Task<CapturaRespuesta> ConsultarAsync(Guid id, CancellationToken cancelacion)
    {
        using var respuesta = await Api.GetAsync(new Uri($"/capturas/{id}", UriKind.Relative), cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.LeerAsync<CapturaRespuesta>())!;
    }

    private async Task<CapturaRespuesta> EsperarFinalAsync(Guid id)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(Cancelacion);
        limite.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var captura = await ConsultarAsync(id, limite.Token);
            if (captura.Estado is EstadoCaptura.Procesada or EstadoCaptura.Fallida) { return captura; }
            await Task.Delay(50, limite.Token);
        }
    }
}
