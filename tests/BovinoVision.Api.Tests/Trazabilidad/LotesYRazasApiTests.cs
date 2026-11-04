using System.Net;
using System.Text.Json;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Aplicacion.Trazabilidad;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Api.Tests.Trazabilidad;

public class LotesYRazasApiTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    [Fact]
    public async Task Razas_devuelve_el_catalogo_sembrado_ordenado_por_nombre()
    {
        var razas = await Cliente.LeerOkAsync<List<RazaRespuesta>>("/razas");

        var esperadas = DatosSemilla.Razas.Select(r => r.Nombre).Order(StringComparer.Ordinal);
        Assert.Equal(esperadas, razas.Select(r => r.Nombre));
        Assert.Contains(razas, r => r.Id == DatosSemilla.Razas[0].Id);
    }

    [Fact]
    public async Task Crear_lote_devuelve_201_con_el_lote_en_snake_case()
    {
        var respuesta = await Cliente.EnviarJsonAsync("/lotes", new { codigo = "L1", nombre = "Recría", gmdObjetivo = 0.8m });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.LeerAsync<JsonElement>();
        Assert.Equal("L1", cuerpo.GetProperty("codigo").GetString());
        Assert.Equal("Recría", cuerpo.GetProperty("nombre").GetString());
        Assert.Equal(0.8m, cuerpo.GetProperty("gmd_objetivo").GetDecimal());
        Assert.Equal(0, cuerpo.GetProperty("cantidad_animales").GetInt32());
        var id = cuerpo.GetProperty("id").GetGuid();
        Assert.Equal(7, id.Version);
        Assert.Equal(new Uri($"/lotes/{id}", UriKind.Relative), respuesta.Headers.Location);

        await using var alcance = Fabrica.Services.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
        var guardado = await contexto.Lotes.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(id, guardado.Id);
        Assert.Equal(DatosSemilla.IdEstablecimiento, guardado.IdEstablecimiento);
    }

    [Fact]
    public async Task Codigo_de_lote_duplicado_devuelve_409()
    {
        await Cliente.CrearLoteAsync("L1");

        var respuesta = await Cliente.EnviarJsonAsync("/lotes", new { codigo = "L1" });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("Ya existe un lote con código L1", (await respuesta.LeerAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Equal("L1", Assert.Single(await Cliente.LeerOkAsync<List<LoteRespuesta>>("/lotes")).Codigo);
    }

    [Fact]
    public async Task Listar_lotes_sin_datos_devuelve_una_lista_vacia()
    {
        Assert.Empty(await Cliente.LeerOkAsync<List<LoteRespuesta>>("/lotes"));
    }

    [Fact]
    public async Task Gmd_objetivo_invalida_devuelve_422_y_no_guarda_nada()
    {
        var respuesta = await Cliente.EnviarJsonAsync("/lotes", new { codigo = "L1", gmdObjetivo = -1m });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal("La GMD objetivo debe ser positiva", (await respuesta.LeerAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Empty(await Cliente.LeerOkAsync<List<LoteRespuesta>>("/lotes"));
    }

    [Fact]
    public async Task Listar_lotes_ordena_por_codigo_y_cuenta_los_animales_vigentes()
    {
        var loteB = await Cliente.CrearLoteAsync("LB");
        var loteA = await Cliente.CrearLoteAsync("LA");
        await Cliente.CrearAnimalAsync("AR001", loteB.Id);
        await Cliente.CrearAnimalAsync("AR002", loteB.Id);
        var movido = await Cliente.CrearAnimalAsync("AR003", loteB.Id);
        await Cliente.ReemplazarJsonAsync($"/animales/{movido.Id}", new { idLote = loteA.Id });

        var lotes = await Cliente.LeerOkAsync<List<LoteRespuesta>>("/lotes");

        Assert.Equal(["LA", "LB"], lotes.Select(l => l.Codigo));
        Assert.Equal(1, lotes.Single(l => l.Id == loteA.Id).CantidadAnimales);
        Assert.Equal(2, lotes.Single(l => l.Id == loteB.Id).CantidadAnimales);
    }

    [Fact]
    public async Task Modificar_lote_solo_cambia_los_valores_enviados()
    {
        var lote = await Cliente.CrearLoteAsync("L1", 0.8m);

        var respuesta = await Cliente.ReemplazarJsonAsync($"/lotes/{lote.Id}", new { nombre = "Engorde" });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var modificado = (await respuesta.LeerAsync<LoteRespuesta>())!;
        Assert.Equal("Engorde", modificado.Nombre);
        Assert.Equal(0.8m, modificado.GmdObjetivo);

        await Cliente.ReemplazarJsonAsync($"/lotes/{lote.Id}", new { gmdObjetivo = 1.2m });

        var guardado = Assert.Single(await Cliente.LeerOkAsync<List<LoteRespuesta>>("/lotes"));
        Assert.Equal("Engorde", guardado.Nombre);
        Assert.Equal(1.2m, guardado.GmdObjetivo);
    }

    [Fact]
    public async Task Modificar_lote_inexistente_devuelve_404()
    {
        var respuesta = await Cliente.ReemplazarJsonAsync($"/lotes/{Guid.CreateVersion7()}", new { nombre = "X" });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
