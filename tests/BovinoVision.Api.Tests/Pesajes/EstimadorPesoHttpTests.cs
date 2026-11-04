using System.Net;
using System.Net.Http.Json;
using System.Text;
using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Aplicacion.Pesajes;
using BovinoVision.Infraestructura.Capturas;
using BovinoVision.Infraestructura.Pesajes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace BovinoVision.Api.Tests.Pesajes;

public sealed class EstimadorPesoHttpTests : IDisposable
{
    private static readonly byte[] Foto = Encoding.ASCII.GetBytes("foto-de-prueba");

    private readonly DirectoryInfo _raiz = Directory.CreateTempSubdirectory("bovino-estimador-");
    private readonly ServicioEstimacionFalso _servicio = new();
    private readonly CancellationToken _cancelacion = TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _servicio.Dispose();
        _raiz.Delete(recursive: true);
    }

    private ServiceProvider Proveedor(Dictionary<string, string?>? estimador = null)
    {
        var valores = new Dictionary<string, string?>
        {
            ["Almacenamiento:DirectorioImagenes"] = _raiz.FullName,
        };
        foreach (var (clave, valor) in estimador ?? new()
        {
            ["Estimador:UrlBase"] = "http://estimador.test",
            ["Estimador:Token"] = "token-prueba",
            ["Estimador:IntervaloConsultaMilisegundos"] = "0",
        })
        {
            valores[clave] = valor;
        }

        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var servicios = new ServiceCollection();
        servicios.AddSingleton<IHostEnvironment>(new HostingEnvironment { ContentRootPath = _raiz.FullName });
        servicios.AgregarInfraestructuraCapturas(configuracion);
        servicios.AgregarInfraestructuraPesajes(configuracion);
        servicios.ConfigureHttpClientDefaults(cliente => cliente.ConfigurePrimaryHttpMessageHandler(() => _servicio));
        return servicios.BuildServiceProvider();
    }

    private async Task<(IEstimadorPeso Estimador, TrabajoInferencia Trabajo)> PrepararAsync(int intento = 1)
    {
        var proveedor = Proveedor();
        var referencia = await proveedor.GetRequiredService<IAlmacenamientoImagenes>().GuardarAsync("foto.jpg", Foto, _cancelacion);
        return (proveedor.GetRequiredService<IEstimadorPeso>(), new TrabajoInferencia(Guid.CreateVersion7(), intento, referencia, "image/jpeg"));
    }

    private static object Completado(decimal? peso, bool confiable, params string[] motivos) => new
    {
        id = "trb_1",
        id_evento = "x",
        estado = "completado",
        peso_kg = peso,
        confiable,
        motivos,
        version_modelo = "modelo-v2",
    };

    [Fact]
    public async Task Envia_la_foto_por_multipart_y_consulta_el_trabajo_hasta_que_termina()
    {
        var (estimador, trabajo) = await PrepararAsync(intento: 2);
        _servicio.Responder(HttpStatusCode.Accepted, new { id = "trb_1", id_evento = "x", estado = "pendiente" });
        _servicio.Responder(HttpStatusCode.OK, new { id = "trb_1", id_evento = "x", estado = "procesando" });
        _servicio.Responder(HttpStatusCode.OK, Completado(412.374m, confiable: true));

        var resultado = await estimador.EstimarAsync(trabajo, _cancelacion);

        Assert.Equal(new ResultadoEstimacion(412.37m, "modelo-v2"), resultado);
        Assert.Collection(_servicio.Pedidos,
            pedido =>
            {
                Assert.Equal("POST http://estimador.test/v1/estimaciones", pedido.Linea);
                Assert.Equal("Bearer token-prueba", pedido.Autorizacion);
                // Cada intento es un trabajo distinto en el servicio.
                Assert.Equal($"{trabajo.IdCaptura:N}-2", pedido.ClaveIdempotencia);
                Assert.Equal("multipart/form-data", pedido.TipoContenido);
                Assert.Contains("name=\"imagenes\"", pedido.Cuerpo, StringComparison.Ordinal);
                Assert.Contains($"filename=\"{trabajo.ReferenciaImagen}\"", pedido.Cuerpo, StringComparison.Ordinal);
                Assert.Contains("Content-Type: image/jpeg", pedido.Cuerpo, StringComparison.Ordinal);
                Assert.Contains("foto-de-prueba", pedido.Cuerpo, StringComparison.Ordinal);
            },
            pedido => Assert.Equal("GET http://estimador.test/v1/estimaciones/trb_1", pedido.Linea),
            pedido =>
            {
                Assert.Equal("GET http://estimador.test/v1/estimaciones/trb_1", pedido.Linea);
                Assert.Equal("Bearer token-prueba", pedido.Autorizacion);
            });
    }

    [Fact]
    public async Task Un_resultado_no_confiable_falla_sin_reintento_con_los_motivos_traducidos()
    {
        var (estimador, trabajo) = await PrepararAsync();
        _servicio.Responder(HttpStatusCode.Accepted, new { id = "trb_1", id_evento = "x", estado = "pendiente" });
        _servicio.Responder(HttpStatusCode.OK, Completado(null, confiable: false, "sin_animal", "fuera_de_rango", "motivo_nuevo"));

        var resultado = await estimador.EstimarAsync(trabajo, _cancelacion);

        Assert.Equal(new ResultadoEstimacion(null, "",
            "No se pudo estimar el peso con confianza: no se detectó ningún bovino; "
            + "el peso estimado está fuera del rango válido del modelo; motivo_nuevo"), resultado);
    }

    [Fact]
    public async Task Un_peso_marcado_como_no_confiable_no_se_registra()
    {
        var (estimador, trabajo) = await PrepararAsync();
        _servicio.Responder(HttpStatusCode.Accepted, new { id = "trb_1", id_evento = "x", estado = "pendiente" });
        _servicio.Responder(HttpStatusCode.OK, Completado(350m, confiable: false));

        var resultado = await estimador.EstimarAsync(trabajo, _cancelacion);

        Assert.Equal(new ResultadoEstimacion(null, "", "No se pudo estimar el peso con confianza"), resultado);
    }

    [Theory]
    [InlineData("tiempo_excedido", true, "La estimación superó el tiempo máximo")]
    [InlineData("referencia_inaccesible", true, "El servicio de estimación no pudo leer la imagen")]
    [InlineData("error_interno", false, "El servicio de estimación falló por un error interno")]
    public async Task Un_trabajo_fallido_conserva_si_es_reintentable(string codigo, bool reintentable, string motivo)
    {
        var (estimador, trabajo) = await PrepararAsync();
        _servicio.Responder(HttpStatusCode.Accepted, new { id = "trb_1", id_evento = "x", estado = "pendiente" });
        _servicio.Responder(HttpStatusCode.OK, new { id = "trb_1", id_evento = "x", estado = "fallido", codigo, detalle = "genérico", reintentable });

        var resultado = await estimador.EstimarAsync(trabajo, _cancelacion);

        Assert.Equal(new ResultadoEstimacion(null, "", motivo, reintentable), resultado);
    }

    [Theory]
    [InlineData(HttpStatusCode.UnsupportedMediaType, "formato_no_admitido", false, "El estimador no admite el formato de la imagen")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "resolucion_insuficiente", false, "La resolución de la foto es insuficiente para estimar el peso")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "codigo_nuevo", false, "Detalle del servicio")]
    [InlineData(HttpStatusCode.Unauthorized, "no_autenticado", true, "El servicio de estimación rechazó las credenciales")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "cola_llena", true, "El servicio de estimación está saturado")]
    public async Task Un_pedido_rechazado_falla_y_solo_se_reintenta_si_el_problema_es_del_servicio(
        HttpStatusCode estado, string codigo, bool reintentable, string motivo)
    {
        var (estimador, trabajo) = await PrepararAsync();
        _servicio.Responder(estado, new { type = "about:blank", title = "x", status = (int)estado, detail = "Detalle del servicio", codigo });

        var resultado = await estimador.EstimarAsync(trabajo, _cancelacion);

        Assert.Equal(new ResultadoEstimacion(null, "", motivo, reintentable), resultado);
        Assert.Single(_servicio.Pedidos);
    }

    [Fact]
    public async Task Un_trabajo_vencido_en_el_servicio_se_reintenta()
    {
        var (estimador, trabajo) = await PrepararAsync();
        _servicio.Responder(HttpStatusCode.Accepted, new { id = "trb_1", id_evento = "x", estado = "pendiente" });
        _servicio.Responder(HttpStatusCode.NotFound, new { status = 404, detail = "El trabajo no existe o ya venció.", codigo = "trabajo_no_encontrado" });

        var resultado = await estimador.EstimarAsync(trabajo, _cancelacion);

        Assert.Equal(new ResultadoEstimacion(null, "", "El trabajo de estimación venció en el servicio", Reintentable: true), resultado);
    }

    [Fact]
    public async Task Una_respuesta_que_no_es_json_se_reintenta()
    {
        var (estimador, trabajo) = await PrepararAsync();
        _servicio.Responder(HttpStatusCode.Accepted, "<html>proxy</html>");

        var resultado = await estimador.EstimarAsync(trabajo, _cancelacion);

        Assert.Equal(new ResultadoEstimacion(null, "", "El servicio de estimación devolvió una respuesta inválida", Reintentable: true), resultado);
    }

    [Fact]
    public async Task Si_la_foto_ya_no_esta_almacenada_falla_sin_llamar_al_servicio()
    {
        var estimador = Proveedor().GetRequiredService<IEstimadorPeso>();

        var resultado = await estimador.EstimarAsync(new TrabajoInferencia(Guid.CreateVersion7(), 1, "no-existe.jpg", "image/jpeg"), _cancelacion);

        Assert.Equal(new ResultadoEstimacion(null, "", "La imagen de la captura ya no está disponible"), resultado);
        Assert.Empty(_servicio.Pedidos);
    }

    [Fact]
    public async Task Sin_url_base_se_sigue_usando_el_estimador_simulado()
    {
        var estimador = Proveedor(new()).GetRequiredService<IEstimadorPeso>();

        var resultado = await estimador.EstimarAsync(new TrabajoInferencia(Guid.CreateVersion7(), 1, "x.jpg", "image/jpeg"), _cancelacion);

        Assert.Equal(new ResultadoEstimacion(400m, "simulado-v1"), resultado);
        Assert.Empty(_servicio.Pedidos);
    }

    [Theory]
    [InlineData("http://estimador.test", "")]
    [InlineData("no-es-una-url", "token")]
    public void Una_configuracion_incompleta_del_estimador_no_arranca(string urlBase, string token)
    {
        using var proveedor = Proveedor(new() { ["Estimador:UrlBase"] = urlBase, ["Estimador:Token"] = token });

        var error = Assert.Throws<OptionsValidationException>(() => proveedor.GetRequiredService<IOptions<OpcionesEstimador>>().Value);

        Assert.Contains("Estimador", error.Message, StringComparison.Ordinal);
    }
}

internal sealed class ServicioEstimacionFalso : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Estado, HttpContent Contenido)> _respuestas = new();

    public sealed record Pedido(string Linea, string? Autorizacion, string? ClaveIdempotencia, string? TipoContenido, string Cuerpo);

    public List<Pedido> Pedidos { get; } = [];

    public void Responder(HttpStatusCode estado, object cuerpo) => _respuestas.Enqueue((estado,
        cuerpo is string texto ? new StringContent(texto, Encoding.UTF8, "text/html") : JsonContent.Create(cuerpo)));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken cancellationToken)
    {
        Pedidos.Add(new(
            $"{pedido.Method} {pedido.RequestUri}",
            pedido.Headers.Authorization?.ToString(),
            pedido.Headers.TryGetValues("Idempotency-Key", out var claves) ? claves.Single() : null,
            pedido.Content?.Headers.ContentType?.MediaType,
            pedido.Content is null ? "" : await pedido.Content.ReadAsStringAsync(cancellationToken)));
        var (estado, contenido) = _respuestas.Dequeue();
        return new HttpResponseMessage(estado) { Content = contenido };
    }
}
