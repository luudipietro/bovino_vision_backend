using System.Text.Json;
using BovinoVision.Api.Tests.Infraestructura;

namespace BovinoVision.Api.Tests.Trazabilidad;

/// <summary>La lista del rodeo y la ficha del animal llevan la foto del último pesaje del animal.</summary>
public class FotoDeAnimalesApiTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    private static readonly DateTimeOffset Inicio = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Es_la_foto_del_ultimo_pesaje_aunque_se_haya_guardado_antes()
    {
        var animal = await Cliente.CrearAnimalAsync("AR-FOTO-01");
        await Fabrica.RegistrarPesajeAsync(animal.Id, 320m, Inicio.AddDays(10));
        await Fabrica.RegistrarPesajeAsync(animal.Id, 300m, Inicio);

        var listado = await Cliente.LeerOkAsync<JsonElement>("/animales");
        var ficha = await Cliente.LeerOkAsync<JsonElement>($"/animales/{animal.Id}");
        var historial = await Cliente.LeerOkAsync<JsonElement>($"/animales/{animal.Id}/pesajes");

        var esperada = historial[0].GetProperty("id_imagen_utilizada").GetGuid();
        Assert.Equal(esperada, Assert.Single(listado.EnumerateArray()).GetProperty("id_imagen_ultimo_pesaje").GetGuid());
        Assert.Equal(esperada, ficha.GetProperty("id_imagen_ultimo_pesaje").GetGuid());
        Assert.NotEqual(historial[1].GetProperty("id_imagen_utilizada").GetGuid(), esperada);
    }

    [Fact]
    public async Task Es_nula_si_el_animal_nunca_se_peso()
    {
        var animal = await Cliente.CrearAnimalAsync("AR-FOTO-02");

        var listado = await Cliente.LeerOkAsync<JsonElement>("/animales");
        var ficha = await Cliente.LeerOkAsync<JsonElement>($"/animales/{animal.Id}");

        Assert.Equal(JsonValueKind.Null, Assert.Single(listado.EnumerateArray()).GetProperty("id_imagen_ultimo_pesaje").ValueKind);
        Assert.Equal(JsonValueKind.Null, ficha.GetProperty("id_imagen_ultimo_pesaje").ValueKind);
    }
}
