using System.Net;
using System.Text.Json;
using BovinoVision.Api.Tests.Analitica;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Api.Tests.Pesajes;

/// <summary>The weighing detail and the history name the user who owns the capture session of each weighing.</summary>
public class OperarioDelPesajeApiTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    private static readonly DateTimeOffset Inicio = DatosAnalitica.Inicio;

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private async Task<JsonElement> ObtenerOkAsync(string ruta)
    {
        using var respuesta = await Cliente.GetAsync(new Uri(ruta, UriKind.Relative), Cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.LeerAsync<JsonElement>();
    }

    [Fact]
    public async Task El_detalle_del_pesaje_trae_el_nombre_del_usuario_dueno_de_la_sesion()
    {
        Guid idEvento;
        await using (var alcance = Fabrica.Services.CreateAsyncScope())
        {
            var datos = new DatosAnalitica(alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>(), Fabrica.IdUsuarioPruebas);
            idEvento = datos.Pesar(datos.CrearAnimal("AR-OPE-01"), 300m, Inicio).Id;
            await datos.GuardarAsync();
        }

        var pesaje = await ObtenerOkAsync($"/pesajes/{idEvento}");

        Assert.Equal("Usuario de pruebas", pesaje.GetProperty("operario").GetString());
    }

    [Fact]
    public async Task El_historial_trae_el_operario_de_cada_pesaje()
    {
        Guid idAnimal;
        await using (var alcance = Fabrica.Services.CreateAsyncScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
            var delSistema = new DatosAnalitica(contexto);
            var delOperario = new DatosAnalitica(contexto, Fabrica.IdUsuarioPruebas);
            var animal = delSistema.CrearAnimal("AR-OPE-02");
            idAnimal = animal.Id;
            delSistema.Pesar(animal, 300m, Inicio);
            delOperario.Pesar(animal, 320m, Inicio.AddDays(10));
            await delSistema.GuardarAsync();
        }

        var historial = await ObtenerOkAsync($"/animales/{idAnimal}/pesajes");

        Assert.Equal(2, historial.GetArrayLength());
        Assert.Equal("Usuario de pruebas", historial[0].GetProperty("operario").GetString());
        Assert.Equal("Sistema", historial[1].GetProperty("operario").GetString());
    }
}
