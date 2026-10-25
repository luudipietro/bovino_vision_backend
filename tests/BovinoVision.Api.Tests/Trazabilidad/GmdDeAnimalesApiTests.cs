using System.Text.Json;
using BovinoVision.Api.Tests.Infraestructura;

namespace BovinoVision.Api.Tests.Trazabilidad;

/// <summary>The herd list and the animal record carry the GMD between the animal's last two weighings.</summary>
public class GmdDeAnimalesApiTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    private static readonly DateTimeOffset Inicio = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    private async Task<(JsonElement Listado, JsonElement Ficha)> LeerAsync(Guid idAnimal)
    {
        var listado = await Cliente.LeerOkAsync<JsonElement>("/animales");
        var ficha = await Cliente.LeerOkAsync<JsonElement>($"/animales/{idAnimal}");
        return (Assert.Single(listado.EnumerateArray()), ficha);
    }

    [Fact]
    public async Task Usa_los_dos_ultimos_pesajes_aunque_se_hayan_guardado_desordenados()
    {
        var animal = await Cliente.CrearAnimalAsync("AR-GMD-01");
        await Fabrica.RegistrarPesajeAsync(animal.Id, 320m, Inicio.AddDays(10));
        await Fabrica.RegistrarPesajeAsync(animal.Id, 100m, Inicio.AddDays(-9));
        await Fabrica.RegistrarPesajeAsync(animal.Id, 300m, Inicio);

        var (listado, ficha) = await LeerAsync(animal.Id);

        Assert.Equal(2.0m, listado.GetProperty("gmd_kg_dia").GetDecimal());
        Assert.Equal(2.0m, ficha.GetProperty("gmd_kg_dia").GetDecimal());
        Assert.Equal(320m, ficha.GetProperty("ultimo_peso_kg").GetDecimal());
    }

    [Fact]
    public async Task Cuenta_dias_calendario_de_la_zona_horaria_igual_que_el_historial()
    {
        // 27 hours apart but two local calendar days (UTC-3): 10 kg over 2 days.
        var animal = await Cliente.CrearAnimalAsync("AR-GMD-02");
        await Fabrica.RegistrarPesajeAsync(animal.Id, 300m, new DateTimeOffset(2026, 1, 10, 1, 0, 0, TimeSpan.Zero));
        await Fabrica.RegistrarPesajeAsync(animal.Id, 310m, new DateTimeOffset(2026, 1, 11, 4, 0, 0, TimeSpan.Zero));

        var (listado, ficha) = await LeerAsync(animal.Id);
        var historial = await Cliente.LeerOkAsync<JsonElement>($"/animales/{animal.Id}/pesajes");

        Assert.Equal(5.0m, listado.GetProperty("gmd_kg_dia").GetDecimal());
        Assert.Equal(5.0m, ficha.GetProperty("gmd_kg_dia").GetDecimal());
        Assert.Equal(historial[0].GetProperty("gmd_kg_dia").GetDecimal(), ficha.GetProperty("gmd_kg_dia").GetDecimal());
    }

    [Fact]
    public async Task Es_nula_con_menos_de_dos_pesajes()
    {
        var sinPesajes = await Cliente.CrearAnimalAsync("AR-GMD-03");
        var (listadoVacio, fichaVacia) = await LeerAsync(sinPesajes.Id);
        await Fabrica.RegistrarPesajeAsync(sinPesajes.Id, 300m, Inicio);

        var (listadoUno, fichaUno) = await LeerAsync(sinPesajes.Id);

        Assert.Equal(JsonValueKind.Null, listadoVacio.GetProperty("gmd_kg_dia").ValueKind);
        Assert.Equal(JsonValueKind.Null, fichaVacia.GetProperty("gmd_kg_dia").ValueKind);
        Assert.Equal(JsonValueKind.Null, listadoUno.GetProperty("gmd_kg_dia").ValueKind);
        Assert.Equal(JsonValueKind.Null, fichaUno.GetProperty("gmd_kg_dia").ValueKind);
    }

    [Fact]
    public async Task Es_nula_si_los_dos_ultimos_pesajes_son_del_mismo_dia()
    {
        var animal = await Cliente.CrearAnimalAsync("AR-GMD-04");
        await Fabrica.RegistrarPesajeAsync(animal.Id, 300m, Inicio);
        await Fabrica.RegistrarPesajeAsync(animal.Id, 320m, Inicio.AddDays(10));
        await Fabrica.RegistrarPesajeAsync(animal.Id, 322m, Inicio.AddDays(10).AddHours(1));

        var (listado, ficha) = await LeerAsync(animal.Id);

        Assert.Equal(JsonValueKind.Null, listado.GetProperty("gmd_kg_dia").ValueKind);
        Assert.Equal(JsonValueKind.Null, ficha.GetProperty("gmd_kg_dia").ValueKind);
    }

    [Fact]
    public async Task Cada_animal_del_listado_trae_su_propia_gmd()
    {
        var a = await Cliente.CrearAnimalAsync("AR-GMD-05");
        var b = await Cliente.CrearAnimalAsync("AR-GMD-06");
        await Fabrica.RegistrarPesajeAsync(a.Id, 300m, Inicio);
        await Fabrica.RegistrarPesajeAsync(a.Id, 310m, Inicio.AddDays(10));
        await Fabrica.RegistrarPesajeAsync(b.Id, 400m, Inicio);
        await Fabrica.RegistrarPesajeAsync(b.Id, 390m, Inicio.AddDays(5));

        var listado = await Cliente.LeerOkAsync<JsonElement>("/animales");

        Assert.Equal(1.0m, listado[0].GetProperty("gmd_kg_dia").GetDecimal());
        Assert.Equal(-2.0m, listado[1].GetProperty("gmd_kg_dia").GetDecimal());
    }
}
