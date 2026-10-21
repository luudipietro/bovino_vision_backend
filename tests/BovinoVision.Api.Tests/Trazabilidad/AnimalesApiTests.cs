using System.Net;
using System.Text.Json;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Aplicacion.Trazabilidad;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Trazabilidad;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Api.Tests.Trazabilidad;

public class AnimalesApiTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    private static readonly DateTimeOffset Enero = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Marzo = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly CancellationToken _cancelacion = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Crear_y_listar_animal()
    {
        var respuesta = await Cliente.EnviarJsonAsync("/animales", new { identificador = "AR001" });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var animales = await Cliente.LeerOkAsync<List<AnimalRespuesta>>("/animales");

        Assert.Equal(["AR001"], animales.Select(a => a.Identificador));
    }

    [Fact]
    public async Task Crear_animal_devuelve_la_ficha_completa_en_snake_case()
    {
        var lote = await Cliente.CrearLoteAsync("L1");
        var raza = DatosSemilla.Razas[1];

        var respuesta = await Cliente.EnviarJsonAsync("/animales", new
        {
            identificador = "AR001",
            idRaza = raza.Id,
            sexo = "hembra",
            fechaNacimiento = "2025-03-01",
            idLote = lote.Id,
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.LeerAsync<JsonElement>();
        var id = cuerpo.GetProperty("id").GetGuid();
        Assert.Equal(7, id.Version);
        Assert.Equal(new Uri($"/animales/{id}", UriKind.Relative), respuesta.Headers.Location);
        Assert.Equal("AR001", cuerpo.GetProperty("identificador").GetString());
        Assert.Equal(DatosSemilla.IdEstablecimiento, cuerpo.GetProperty("id_establecimiento").GetGuid());
        Assert.Equal(lote.Id, cuerpo.GetProperty("id_lote").GetGuid());
        Assert.Equal("L1", cuerpo.GetProperty("lote_codigo").GetString());
        Assert.Equal(raza.Id, cuerpo.GetProperty("id_raza").GetGuid());
        Assert.Equal(raza.Nombre, cuerpo.GetProperty("raza").GetString());
        Assert.Equal("hembra", cuerpo.GetProperty("sexo").GetString());
        Assert.Equal("2025-03-01", cuerpo.GetProperty("fecha_nacimiento").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("ultimo_peso_kg").ValueKind);
        Assert.Equal(0, cuerpo.GetProperty("alertas_activas").GetInt32());

        await using var alcance = Fabrica.Services.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
        var asignacion = await contexto.HistorialLotes.SingleAsync(_cancelacion);
        Assert.Equal(id, asignacion.IdAnimal);
        Assert.Equal(lote.Id, asignacion.IdLote);
        Assert.Equal(Fabrica.IdUsuarioPruebas, asignacion.IdUsuario);
        Assert.Null(asignacion.VigenteHasta);
    }

    [Fact]
    public async Task Identificador_duplicado_devuelve_409()
    {
        await Cliente.CrearAnimalAsync("AR001");

        var respuesta = await Cliente.EnviarJsonAsync("/animales", new { identificador = "AR001" });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("Ya existe un animal con identificador AR001", (await respuesta.LeerAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Equal("AR001", Assert.Single(await Cliente.LeerOkAsync<List<AnimalRespuesta>>("/animales")).Identificador);
    }

    [Fact]
    public async Task Identificador_vacio_devuelve_422()
    {
        var respuesta = await Cliente.EnviarJsonAsync("/animales", new { identificador = " " });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
    }

    [Fact]
    public async Task Ficha_de_animal_inexistente_devuelve_404()
    {
        var respuesta = await Cliente.GetAsync(new Uri($"/animales/{Guid.CreateVersion7()}", UriKind.Relative), _cancelacion);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_lote_y_asignar_a_animal()
    {
        var lote = await Cliente.CrearLoteAsync("L1");

        var animal = await Cliente.CrearAnimalAsync("AR002", lote.Id);

        Assert.Equal(lote.Id, animal.IdLote);
    }

    [Fact]
    public async Task Asignar_lote_inexistente_devuelve_404_y_no_guarda_el_animal()
    {
        var respuesta = await Cliente.EnviarJsonAsync("/animales", new { identificador = "AR003", idLote = Guid.CreateVersion7() });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Empty(await Cliente.LeerOkAsync<List<AnimalRespuesta>>("/animales"));
    }

    [Fact]
    public async Task Raza_inexistente_devuelve_404()
    {
        var respuesta = await Cliente.EnviarJsonAsync("/animales", new { identificador = "AR003", idRaza = Guid.CreateVersion7() });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Modificar_animal_solo_cambia_los_valores_enviados()
    {
        var raza = DatosSemilla.Razas[0];
        var creado = await Cliente.EnviarJsonAsync("/animales", new { identificador = "AR001", idRaza = raza.Id, sexo = "macho" });
        var animal = (await creado.LeerAsync<AnimalRespuesta>())!;

        var respuesta = await Cliente.ReemplazarJsonAsync($"/animales/{animal.Id}", new { fechaNacimiento = "2025-01-15" });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var ficha = await Cliente.LeerOkAsync<AnimalRespuesta>($"/animales/{animal.Id}");
        Assert.Equal(raza.Id, ficha.IdRaza);
        Assert.Equal(Sexo.Macho, ficha.Sexo);
        Assert.Equal(new DateOnly(2025, 1, 15), ficha.FechaNacimiento);
    }

    [Fact]
    public async Task Modificar_animal_inexistente_devuelve_404()
    {
        var respuesta = await Cliente.ReemplazarJsonAsync($"/animales/{Guid.CreateVersion7()}", new { sexo = "macho" });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Modificar_con_lote_o_raza_inexistente_devuelve_404()
    {
        var animal = await Cliente.CrearAnimalAsync("AR001");

        var sinLote = await Cliente.ReemplazarJsonAsync($"/animales/{animal.Id}", new { idLote = Guid.CreateVersion7() });
        var sinRaza = await Cliente.ReemplazarJsonAsync($"/animales/{animal.Id}", new { idRaza = Guid.CreateVersion7() });

        Assert.Equal(HttpStatusCode.NotFound, sinLote.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, sinRaza.StatusCode);
    }

    [Fact]
    public async Task Cambiar_de_lote_cierra_la_asignacion_vigente_y_abre_otra_en_la_misma_transaccion()
    {
        var loteA = await Cliente.CrearLoteAsync("LA");
        var loteB = await Cliente.CrearLoteAsync("LB");
        var animal = await Cliente.CrearAnimalAsync("AR001", loteA.Id);

        var respuesta = await Cliente.ReemplazarJsonAsync($"/animales/{animal.Id}", new { idLote = loteB.Id });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var modificado = (await respuesta.LeerAsync<AnimalRespuesta>())!;
        Assert.Equal(loteB.Id, modificado.IdLote);
        Assert.Equal("LB", modificado.LoteCodigo);

        await using var alcance = Fabrica.Services.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
        var historial = await contexto.HistorialLotes.Where(h => h.IdAnimal == animal.Id).ToListAsync(_cancelacion);
        Assert.Equal(2, historial.Count);
        var cerrada = historial.Single(h => h.IdLote == loteA.Id);
        var abierta = historial.Single(h => h.IdLote == loteB.Id);
        Assert.NotNull(cerrada.VigenteHasta);
        Assert.Equal(cerrada.VigenteHasta, abierta.VigenteDesde);
        Assert.Null(abierta.VigenteHasta);
    }

    [Fact]
    public async Task Reasignar_el_mismo_lote_no_registra_nada()
    {
        var lote = await Cliente.CrearLoteAsync("L1");
        var animal = await Cliente.CrearAnimalAsync("AR001", lote.Id);

        var respuesta = await Cliente.ReemplazarJsonAsync($"/animales/{animal.Id}", new { idLote = lote.Id });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var historial = await Cliente.LeerOkAsync<List<AsignacionLoteRespuesta>>($"/animales/{animal.Id}/historial-lotes");
        Assert.Null(Assert.Single(historial).VigenteHasta);
    }

    [Fact]
    public async Task Historial_de_lotes_devuelve_las_asignaciones_de_la_mas_reciente_a_la_mas_antigua()
    {
        var loteA = await Cliente.CrearLoteAsync("LA");
        var loteB = await Cliente.CrearLoteAsync("LB");
        var animal = await Cliente.CrearAnimalAsync("AR001", loteA.Id);
        await Cliente.ReemplazarJsonAsync($"/animales/{animal.Id}", new { idLote = loteB.Id });

        var respuesta = await Cliente.GetAsync(new Uri($"/animales/{animal.Id}/historial-lotes", UriKind.Relative), _cancelacion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.LeerAsync<JsonElement>();
        Assert.Equal(["LB", "LA"], cuerpo.EnumerateArray().Select(h => h.GetProperty("lote_codigo").GetString()));
        var actual = cuerpo[0];
        Assert.Equal(loteB.Id, actual.GetProperty("id_lote").GetGuid());
        Assert.Equal(JsonValueKind.Null, actual.GetProperty("vigente_hasta").ValueKind);
        Assert.Equal(actual.GetProperty("vigente_desde").GetDateTimeOffset(), cuerpo[1].GetProperty("vigente_hasta").GetDateTimeOffset());
    }

    [Fact]
    public async Task Historial_de_lotes_de_animal_inexistente_devuelve_404()
    {
        var respuesta = await Cliente.GetAsync(new Uri($"/animales/{Guid.CreateVersion7()}/historial-lotes", UriKind.Relative), _cancelacion);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Listar_filtra_por_lote_vigente_y_por_identificador_sin_distinguir_mayusculas()
    {
        var loteA = await Cliente.CrearLoteAsync("LA");
        var loteB = await Cliente.CrearLoteAsync("LB");
        await Cliente.CrearAnimalAsync("AR-002", loteA.Id);
        await Cliente.CrearAnimalAsync("ar-001", loteA.Id);
        await Cliente.CrearAnimalAsync("BR-003", loteA.Id);
        var movido = await Cliente.CrearAnimalAsync("AR-004", loteA.Id);
        await Cliente.ReemplazarJsonAsync($"/animales/{movido.Id}", new { idLote = loteB.Id });

        var delLoteA = await Cliente.LeerOkAsync<List<AnimalRespuesta>>($"/animales?id_lote={loteA.Id}");
        var buscados = await Cliente.LeerOkAsync<List<AnimalRespuesta>>("/animales?q=Ar-");
        var combinados = await Cliente.LeerOkAsync<List<AnimalRespuesta>>($"/animales?id_lote={loteA.Id}&q=ar");

        Assert.Equal(["AR-002", "BR-003", "ar-001"], delLoteA.Select(a => a.Identificador).Order(StringComparer.Ordinal));
        Assert.Equal(["AR-002", "AR-004", "ar-001"], buscados.Select(a => a.Identificador).Order(StringComparer.Ordinal));
        Assert.Equal(["AR-002", "ar-001"], combinados.Select(a => a.Identificador).Order(StringComparer.Ordinal));
        Assert.Empty(await Cliente.LeerOkAsync<List<AnimalRespuesta>>("/animales?q=%25"));
        Assert.Empty(await Cliente.LeerOkAsync<List<AnimalRespuesta>>("/animales?q=_"));
    }

    [Fact]
    public async Task Listar_ordena_por_identificador()
    {
        await Cliente.CrearAnimalAsync("AR003");
        await Cliente.CrearAnimalAsync("AR001");
        await Cliente.CrearAnimalAsync("AR002");

        var animales = await Cliente.LeerOkAsync<List<AnimalRespuesta>>("/animales");

        Assert.Equal(["AR001", "AR002", "AR003"], animales.Select(a => a.Identificador));
    }

    [Fact]
    public async Task Listado_y_ficha_incluyen_el_ultimo_peso_por_fecha_de_pesaje()
    {
        var animal = await Cliente.CrearAnimalAsync("AR-PESO-01");
        Assert.Null(animal.UltimoPesoKg);

        // Inserted out of order: the latest fecha_pesaje wins, not the latest row.
        await Fabrica.RegistrarPesajeAsync(animal.Id, 320m, Marzo);
        await Fabrica.RegistrarPesajeAsync(animal.Id, 300m, Enero);

        var ficha = await Cliente.LeerOkAsync<AnimalRespuesta>($"/animales/{animal.Id}");
        var listado = await Cliente.LeerOkAsync<List<AnimalRespuesta>>("/animales");

        Assert.Equal(320m, ficha.UltimoPesoKg);
        Assert.Equal(320m, Assert.Single(listado).UltimoPesoKg);
    }

    [Fact]
    public async Task Listado_cuenta_solo_alertas_activas_y_filtra_por_ellas()
    {
        var conAlertas = await Cliente.CrearAnimalAsync("AR001");
        var conResuelta = await Cliente.CrearAnimalAsync("AR002");
        await Cliente.CrearAnimalAsync("AR003");
        await Fabrica.RegistrarPesajeAsync(conAlertas.Id, 300m, Enero, EstadoAlerta.Activa);
        await Fabrica.RegistrarPesajeAsync(conAlertas.Id, 290m, Marzo, EstadoAlerta.Activa);
        await Fabrica.RegistrarPesajeAsync(conResuelta.Id, 300m, Enero, EstadoAlerta.Resuelta);

        var todos = await Cliente.LeerOkAsync<List<AnimalRespuesta>>("/animales");
        var soloConAlertas = await Cliente.LeerOkAsync<List<AnimalRespuesta>>("/animales?con_alertas_activas=true");
        var ficha = await Cliente.LeerOkAsync<AnimalRespuesta>($"/animales/{conAlertas.Id}");

        Assert.Equal([2, 0, 0], todos.Select(a => a.AlertasActivas));
        Assert.Equal(conAlertas.Id, Assert.Single(soloConAlertas).Id);
        Assert.Equal(2, ficha.AlertasActivas);
        Assert.Equal(290m, ficha.UltimoPesoKg);
    }
}
