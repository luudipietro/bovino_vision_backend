using System.Net;
using System.Text.Json;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Aplicacion.Trazabilidad;

namespace BovinoVision.Api.Tests.Trazabilidad;

public class PersistenciaHistorialApiTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    [Fact]
    public async Task Cambios_sucesivos_conservan_el_historial_y_un_rechazo_no_lo_modifica()
    {
        var loteA = await Cliente.CrearLoteAsync("LA");
        var loteB = await Cliente.CrearLoteAsync("LB");
        var animal = await Cliente.CrearAnimalAsync("AR001", loteA.Id);
        var ruta = $"/animales/{animal.Id}";

        foreach (var lote in new[] { loteB, loteA })
        {
            var respuesta = await Cliente.ReemplazarJsonAsync(ruta, new { idLote = lote.Id });
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.Equal(lote.Id, (await respuesta.LeerAsync<AnimalRespuesta>())!.IdLote);
        }

        var historial = await Cliente.LeerOkAsync<List<AsignacionLoteRespuesta>>($"{ruta}/historial-lotes");
        Assert.Equal(new[] { loteA.Id, loteB.Id, loteA.Id }, historial.Select(h => h.IdLote));
        Assert.Null(historial[0].VigenteHasta);
        Assert.Equal(historial[0].VigenteDesde, historial[1].VigenteHasta);
        Assert.Equal(historial[1].VigenteDesde, historial[2].VigenteHasta);

        var inexistente = Guid.CreateVersion7();
        var rechazo = await Cliente.ReemplazarJsonAsync(ruta, new { idLote = inexistente, sexo = "macho" });
        Assert.Equal(HttpStatusCode.NotFound, rechazo.StatusCode);
        Assert.Equal($"No existe el lote {inexistente}", (await rechazo.LeerAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Equal(historial, await Cliente.LeerOkAsync<List<AsignacionLoteRespuesta>>($"{ruta}/historial-lotes"));
        var ficha = await Cliente.LeerOkAsync<AnimalRespuesta>(ruta);
        Assert.Equal(loteA.Id, ficha.IdLote);
        Assert.Null(ficha.Sexo);
        var lotes = await Cliente.LeerOkAsync<List<LoteRespuesta>>("/lotes");
        Assert.Equal(1, lotes.Single(l => l.Id == loteA.Id).CantidadAnimales);
        Assert.Equal(0, lotes.Single(l => l.Id == loteB.Id).CantidadAnimales);
    }
}
