using System.Net;
using System.Text.Json;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Aplicacion.Analitica;
using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BovinoVision.Api.Tests.Analitica;

public class AnaliticaEndpointsTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    private static readonly DateTimeOffset Inicio = DatosAnalitica.Inicio;

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private async Task Preparar(Action<DatosAnalitica> preparar)
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        var datos = new DatosAnalitica(alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>());
        preparar(datos);
        await datos.GuardarAsync();
    }

    private async Task<(HttpStatusCode Estado, JsonElement Cuerpo)> Obtener(string ruta)
    {
        var respuesta = await Cliente.GetAsync(new Uri(ruta, UriKind.Relative), Cancelacion);
        var cuerpo = respuesta.Content.Headers.ContentLength == 0 ? default : await respuesta.LeerAsync<JsonElement>();
        return (respuesta.StatusCode, cuerpo);
    }

    [Fact]
    public async Task El_historial_trae_los_pesajes_del_mas_reciente_al_mas_antiguo_con_gmd_y_lote_historico()
    {
        Guid idAnimal = default;
        Guid idImagenUltima = default;
        await Preparar(datos =>
        {
            var loteA = datos.CrearLote("L-A");
            var loteB = datos.CrearLote("L-B");
            var animal = datos.CrearAnimal("AR-HIST-01");
            idAnimal = animal.Id;
            DatosAnalitica.AsignarLote(animal, loteA, Inicio.AddDays(-1));
            datos.Pesar(animal, 300m, Inicio);
            DatosAnalitica.AsignarLote(animal, loteB, Inicio.AddDays(5));
            idImagenUltima = datos.Pesar(animal, 320m, Inicio.AddDays(10)).IdImagenUtilizada;
        });

        var (estado, cuerpo) = await Obtener($"/animales/{idAnimal}/pesajes");

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal(2, cuerpo.GetArrayLength());
        var ultimo = cuerpo[0];
        Assert.Equal(320m, ultimo.GetProperty("peso_kg").GetDecimal());
        Assert.Equal(2.0m, ultimo.GetProperty("gmd_kg_dia").GetDecimal());
        Assert.Equal("L-B", ultimo.GetProperty("lote_codigo").GetString());
        Assert.Equal("fake-v1", ultimo.GetProperty("version_modelo").GetString());
        Assert.Equal(idImagenUltima, ultimo.GetProperty("id_imagen_utilizada").GetGuid());
        Assert.Equal(Inicio.AddDays(10), ultimo.GetProperty("fecha_pesaje").GetDateTimeOffset());
        Assert.Equal(Inicio.AddDays(10).AddMinutes(5), ultimo.GetProperty("fecha_procesamiento").GetDateTimeOffset());
        Assert.True(ultimo.TryGetProperty("id", out _));
        Assert.Equal("L-A", cuerpo[1].GetProperty("lote_codigo").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo[1].GetProperty("gmd_kg_dia").ValueKind);
    }

    [Fact]
    public async Task El_historial_no_tiene_gmd_para_pesajes_del_mismo_dia()
    {
        // Port of test_historial_incluye_gmd: both weighings on the same day give no GMD.
        Guid idAnimal = default;
        await Preparar(datos =>
        {
            var animal = datos.CrearAnimal("AR-GMD-01");
            idAnimal = animal.Id;
            datos.Pesar(animal, 300m, Inicio);
            datos.Pesar(animal, 320m, Inicio.AddHours(2));
        });

        var (_, cuerpo) = await Obtener($"/animales/{idAnimal}/pesajes");

        Assert.All(cuerpo.EnumerateArray(), p => Assert.Equal(JsonValueKind.Null, p.GetProperty("gmd_kg_dia").ValueKind));
        Assert.Equal(JsonValueKind.Null, cuerpo[0].GetProperty("lote_codigo").ValueKind);
    }

    [Theory]
    [InlineData("pesajes")]
    [InlineData("alertas")]
    public async Task Las_rutas_de_un_animal_inexistente_devuelven_404(string recurso)
    {
        var (estado, cuerpo) = await Obtener($"/animales/{Guid.CreateVersion7()}/{recurso}");

        Assert.Equal(HttpStatusCode.NotFound, estado);
        Assert.Contains("No existe el animal", cuerpo.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_perdida_de_peso_crea_una_alerta_visible_en_el_animal_y_en_el_rodeo()
    {
        // Port of test_variacion_grande_crea_alerta_visible_por_api, adapted to the GMD rule.
        Guid idAnimal;
        Guid idEvento;
        await using (var alcance = Fabrica.Services.CreateAsyncScope())
        {
            var datos = new DatosAnalitica(alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>());
            var animal = datos.CrearAnimal("AR-ALERTA-01");
            idAnimal = animal.Id;
            datos.Pesar(animal, 450m, Inicio);
            await datos.GuardarAsync();

            var evento = datos.Pesar(animal, 300m, Inicio.AddDays(10));
            idEvento = evento.Id;
            await alcance.ServiceProvider.GetRequiredService<IEvaluadorAlertas>().EvaluarAsync(evento, Cancelacion);
            await alcance.ServiceProvider.GetRequiredService<IUnidadDeTrabajo>().GuardarCambiosAsync(Cancelacion);
        }

        var (estado, delAnimal) = await Obtener($"/animales/{idAnimal}/alertas");
        var (_, delRodeo) = await Obtener("/alertas");

        Assert.Equal(HttpStatusCode.OK, estado);
        var alerta = Assert.Single(delAnimal.EnumerateArray());
        Assert.Equal(idEvento, alerta.GetProperty("id_evento").GetGuid());
        Assert.Equal(idAnimal, alerta.GetProperty("id_animal").GetGuid());
        Assert.Equal("AR-ALERTA-01", alerta.GetProperty("animal_identificador").GetString());
        Assert.Equal("perdida_peso", alerta.GetProperty("tipo").GetString());
        Assert.Equal(1, alerta.GetProperty("prioridad").GetInt32());
        Assert.Equal(-15m, alerta.GetProperty("variacion").GetDecimal());
        Assert.Equal(JsonValueKind.Null, alerta.GetProperty("gmd_esperada").ValueKind);
        Assert.Equal("activa", alerta.GetProperty("estado").GetString());
        Assert.True(alerta.TryGetProperty("fecha_generacion", out _));
        Assert.True(alerta.TryGetProperty("id", out _));
        Assert.Equal("AR-ALERTA-01", Assert.Single(delRodeo.EnumerateArray()).GetProperty("animal_identificador").GetString());
    }

    [Fact]
    public async Task Un_aumento_normal_no_crea_alerta()
    {
        // Port of test_variacion_chica_no_crea_alerta.
        Guid idAnimal;
        await using (var alcance = Fabrica.Services.CreateAsyncScope())
        {
            var datos = new DatosAnalitica(alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>());
            var animal = datos.CrearAnimal("AR-ALERTA-02");
            idAnimal = animal.Id;
            datos.Pesar(animal, 300m, Inicio);
            await datos.GuardarAsync();

            var evento = datos.Pesar(animal, 305m, Inicio.AddDays(10));
            Assert.Null(await alcance.ServiceProvider.GetRequiredService<IEvaluadorAlertas>().EvaluarAsync(evento, Cancelacion));
            await datos.GuardarAsync();
        }

        var (_, cuerpo) = await Obtener($"/animales/{idAnimal}/alertas");

        Assert.Equal(0, cuerpo.GetArrayLength());
    }

    [Fact]
    public async Task Las_alertas_del_rodeo_se_ordenan_por_prioridad_y_luego_por_fecha_descendente()
    {
        var esperadas = new List<Guid>();
        await Preparar(datos =>
        {
            var animal = datos.CrearAnimal("AR-PRIO-01");
            var e1 = datos.Pesar(animal, 300m, Inicio);
            var e2 = datos.Pesar(animal, 301m, Inicio.AddDays(1));
            var e3 = datos.Pesar(animal, 302m, Inicio.AddDays(2));
            var estancamiento = datos.Alertar(e1, TipoAlerta.Estancamiento, 2, Inicio.AddDays(3));
            var perdidaVieja = datos.Alertar(e2, TipoAlerta.PerdidaPeso, 1, Inicio.AddDays(1));
            var perdidaNueva = datos.Alertar(e3, TipoAlerta.PerdidaPeso, 1, Inicio.AddDays(2));
            esperadas.AddRange([perdidaNueva.Id, perdidaVieja.Id, estancamiento.Id]);
        });

        var (estado, cuerpo) = await Obtener("/alertas");

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal(esperadas, cuerpo.EnumerateArray().Select(a => a.GetProperty("id").GetGuid()));
        Assert.Equal("estancamiento", cuerpo[2].GetProperty("tipo").GetString());
        Assert.Equal(1.2m, cuerpo[2].GetProperty("gmd_esperada").GetDecimal());
    }

    [Fact]
    public async Task El_panel_agrega_el_ultimo_peso_y_la_gmd_de_cada_animal()
    {
        // Port of test_panel_del_rodeo_agrega_datos.
        await Preparar(datos =>
        {
            var animal1 = datos.CrearAnimal("AR-PANEL-01");
            var animal2 = datos.CrearAnimal("AR-PANEL-02");
            datos.Pesar(animal1, 300m, Inicio);
            datos.Pesar(animal1, 320m, Inicio.AddDays(10));
            datos.Pesar(animal2, 400m, Inicio);
        });

        var (estado, panel) = await Obtener("/panel");

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal(2, panel.GetProperty("cantidad_animales").GetInt32());
        Assert.Equal(360.0m, panel.GetProperty("peso_promedio_kg").GetDecimal());
        Assert.Equal(2.0m, panel.GetProperty("gmd_promedio_kg_dia").GetDecimal());
        Assert.Equal(1, panel.GetProperty("animales_con_gmd").GetInt32());
        var evolucion = panel.GetProperty("evolucion_peso_promedio");
        Assert.Equal(2, evolucion.GetArrayLength());
        Assert.Equal("2026-01-01", evolucion[0].GetProperty("fecha").GetString());
        Assert.Equal(350.0m, evolucion[0].GetProperty("peso_promedio_kg").GetDecimal());
        Assert.Equal("2026-01-11", evolucion[1].GetProperty("fecha").GetString());
    }

    [Fact]
    public async Task El_panel_y_el_reporte_filtran_por_el_lote_historico_del_pesaje()
    {
        Guid idLoteA = default;
        await Preparar(datos =>
        {
            var loteA = datos.CrearLote("L-A");
            var loteB = datos.CrearLote("L-B");
            idLoteA = loteA.Id;
            var animal = datos.CrearAnimal("AR-LOTE-01");
            DatosAnalitica.AsignarLote(animal, loteA, Inicio.AddDays(-1));
            datos.Pesar(animal, 300m, Inicio);
            datos.Pesar(animal, 310m, Inicio.AddDays(5));
            DatosAnalitica.AsignarLote(animal, loteB, Inicio.AddDays(6));
            datos.Pesar(animal, 400m, Inicio.AddDays(10));
            datos.Pesar(datos.CrearAnimal("AR-SIN-LOTE"), 500m, Inicio);
        });

        var (_, panel) = await Obtener($"/panel?id_lote={idLoteA}");
        var (_, reporte) = await Obtener($"/reportes?desde=2026-01-01&hasta=2026-01-31&id_lote={idLoteA}");

        Assert.Equal(1, panel.GetProperty("cantidad_animales").GetInt32());
        Assert.Equal(310m, panel.GetProperty("peso_promedio_kg").GetDecimal());
        Assert.Equal(2.0m, panel.GetProperty("gmd_promedio_kg_dia").GetDecimal());
        Assert.Equal(2, reporte.GetProperty("cantidad_pesajes").GetInt32());
        Assert.Equal(1, reporte.GetProperty("cantidad_animales").GetInt32());
        Assert.Equal(305m, reporte.GetProperty("peso_promedio_kg").GetDecimal());
    }

    [Fact]
    public async Task El_reporte_filtra_por_periodo_inclusivo()
    {
        // Port of test_reporte_filtra_por_lapso_de_tiempo.
        await Preparar(datos =>
        {
            var animal1 = datos.CrearAnimal("AR-REPORTE-01");
            var animal2 = datos.CrearAnimal("AR-REPORTE-02");
            datos.Pesar(animal1, 300m, Inicio);
            datos.Pesar(animal1, 330m, Inicio.AddDays(10));
            datos.Pesar(animal2, 400m, Inicio.AddDays(10));
            datos.Pesar(animal1, 999m, Inicio.AddDays(40));
        });

        var (_, vacio) = await Obtener("/reportes?desde=2020-01-01&hasta=2020-01-02");
        var (estado, reporte) = await Obtener("/reportes?desde=2026-01-01&hasta=2026-01-11");

        Assert.Equal(0, vacio.GetProperty("cantidad_pesajes").GetInt32());
        Assert.Equal(0, vacio.GetProperty("cantidad_animales").GetInt32());
        Assert.Equal(JsonValueKind.Null, vacio.GetProperty("peso_promedio_kg").ValueKind);
        Assert.Equal(JsonValueKind.Null, vacio.GetProperty("gmd_promedio_kg_dia").ValueKind);
        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal("2026-01-01", reporte.GetProperty("desde").GetString());
        Assert.Equal("2026-01-11", reporte.GetProperty("hasta").GetString());
        Assert.Equal(3, reporte.GetProperty("cantidad_pesajes").GetInt32());
        Assert.Equal(2, reporte.GetProperty("cantidad_animales").GetInt32());
        Assert.Equal(343.3m, reporte.GetProperty("peso_promedio_kg").GetDecimal());
        Assert.Equal(3.0m, reporte.GetProperty("gmd_promedio_kg_dia").GetDecimal());
    }

    [Fact]
    public async Task El_reporte_con_desde_posterior_a_hasta_devuelve_422()
    {
        var (estado, cuerpo) = await Obtener("/reportes?desde=2026-02-01&hasta=2026-01-01");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, estado);
        Assert.Contains("desde", cuerpo.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_panel_no_reutiliza_una_gmd_antigua_si_el_ultimo_intervalo_es_del_mismo_dia()
    {
        await Preparar(datos =>
        {
            var animal = datos.CrearAnimal("AR-ULTIMO-INTERVALO");
            datos.Pesar(animal, 300m, Inicio);
            datos.Pesar(animal, 320m, Inicio.AddDays(10));
            datos.Pesar(animal, 322m, Inicio.AddDays(10).AddHours(1));
        });

        var (estado, panel) = await Obtener("/panel");

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal(322m, panel.GetProperty("peso_promedio_kg").GetDecimal());
        Assert.Equal(JsonValueKind.Null, panel.GetProperty("gmd_promedio_kg_dia").ValueKind);
        Assert.Equal(0, panel.GetProperty("animales_con_gmd").GetInt32());
    }

    [Fact]
    public async Task El_reporte_incluye_el_dia_local_completo_sin_incluir_el_siguiente()
    {
        await Preparar(datos =>
        {
            var animal = datos.CrearAnimal("AR-LIMITES");
            var comienzo = new DateTimeOffset(2026, 1, 1, 3, 0, 0, TimeSpan.Zero);
            datos.Pesar(animal, 100m, comienzo.AddTicks(-10));
            datos.Pesar(animal, 300m, comienzo);
            datos.Pesar(animal, 400m, comienzo.AddDays(1).AddTicks(-10));
            datos.Pesar(animal, 900m, comienzo.AddDays(1));
        });

        var (estado, reporte) = await Obtener("/reportes?desde=2026-01-01&hasta=2026-01-01");

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal(2, reporte.GetProperty("cantidad_pesajes").GetInt32());
        Assert.Equal(1, reporte.GetProperty("cantidad_animales").GetInt32());
        Assert.Equal(350m, reporte.GetProperty("peso_promedio_kg").GetDecimal());
        Assert.Equal(JsonValueKind.Null, reporte.GetProperty("gmd_promedio_kg_dia").ValueKind);
    }

    [Fact]
    public async Task Un_animal_sin_pesajes_tiene_historial_y_alertas_vacios_y_no_aporta_al_panel()
    {
        Guid idAnimal = default;
        await Preparar(datos => idAnimal = datos.CrearAnimal("AR-VACIO").Id);

        var (estadoPesajes, pesajes) = await Obtener($"/animales/{idAnimal}/pesajes");
        var (estadoAlertas, alertas) = await Obtener($"/animales/{idAnimal}/alertas");
        var (estadoPanel, panel) = await Obtener("/panel");

        Assert.Equal(HttpStatusCode.OK, estadoPesajes);
        Assert.Equal(HttpStatusCode.OK, estadoAlertas);
        Assert.Equal(HttpStatusCode.OK, estadoPanel);
        Assert.Equal(0, pesajes.GetArrayLength());
        Assert.Equal(0, alertas.GetArrayLength());
        Assert.Equal(0, panel.GetProperty("cantidad_animales").GetInt32());
        Assert.Equal(0, panel.GetProperty("animales_con_gmd").GetInt32());
        Assert.Equal(JsonValueKind.Null, panel.GetProperty("peso_promedio_kg").ValueKind);
        Assert.Equal(JsonValueKind.Null, panel.GetProperty("gmd_promedio_kg_dia").ValueKind);
        Assert.Equal(0, panel.GetProperty("evolucion_peso_promedio").GetArrayLength());
    }

    [Fact]
    public async Task Las_lecturas_usan_un_numero_constante_de_consultas_sin_n_mas_uno()
    {
        Guid idAnimal = default;
        await Preparar(datos =>
        {
            for (var i = 0; i < 10; i++)
            {
                var animal = datos.CrearAnimal($"AR-BATCH-{i}");
                idAnimal = animal.Id;
                datos.Pesar(animal, 320m, Inicio);
                var evento = datos.Pesar(animal, 300m, Inicio.AddDays(10));
                datos.Alertar(evento, TipoAlerta.PerdidaPeso, 1, Inicio.AddDays(10));
            }
        });

        using var contador = new ContadorConsultas();
        await using var fabrica = Fabrica.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
            logging.AddProvider(contador).AddFilter<ContadorConsultas>(
                "Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information)));
        using var cliente = fabrica.CreateClient();
        (string Ruta, int Consultas)[] rutas =
        [
            ("/panel", 1),
            ("/reportes?desde=2026-01-01&hasta=2026-01-31", 1),
            ("/alertas", 1),
            ($"/animales/{idAnimal}/pesajes", 2),
            ($"/animales/{idAnimal}/alertas", 2),
        ];
        foreach (var (ruta, consultas) in rutas)
        {
            contador.Reiniciar();
            using var respuesta = await cliente.GetAsync(new Uri(ruta, UriKind.Relative), Cancelacion);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.Equal(consultas, contador.Cantidad);
        }
    }

    [Fact]
    public async Task El_reporte_sin_fechas_devuelve_400()
    {
        var respuesta = await Cliente.GetAsync(new Uri("/reportes?desde=2026-01-01", UriKind.Relative), Cancelacion);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        // Same ProblemDetails shape as domain errors, in every environment (Development used to answer 500).
        var cuerpo = await respuesta.LeerAsync<JsonElement>();
        Assert.Equal(400, cuerpo.GetProperty("status").GetInt32());
        Assert.Contains("hasta", cuerpo.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    private sealed class ContadorConsultas : ILoggerProvider, ILogger
    {
        private int _cantidad;
        public int Cantidad => Volatile.Read(ref _cantidad);
        public void Reiniciar() => Interlocked.Exchange(ref _cantidad, 0);
        public ILogger CreateLogger(string categoryName) => this;
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (eventId.Id == 20101)
            {
                Interlocked.Increment(ref _cantidad);
            }
        }

        public void Dispose() { }
    }
}
