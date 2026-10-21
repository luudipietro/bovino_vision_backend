using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BovinoVision.Api.Tests.Capturas;

public class CapturasApiTests(FabricaApi fabrica) : PruebaDeCapturas(fabrica)
{
    [Fact]
    public async Task Iniciar_sesion_devuelve_201_para_el_usuario_actual()
    {
        using var respuesta = await ClienteCapturas.PostAsync(new Uri("/sesiones-captura", UriKind.Relative), null, Cancelacion);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.LeerAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, cuerpo.GetProperty("id").GetGuid());
        Assert.Equal(DatosSemilla.IdUsuarioSistema, cuerpo.GetProperty("id_usuario").GetGuid());
        Assert.Equal("activa", cuerpo.GetProperty("estado").GetString());
        Assert.True(cuerpo.TryGetProperty("fecha_inicio", out _));
    }

    [Fact]
    public async Task Subir_una_captura_con_fecha_futura_responde_422_sin_guardarla()
    {
        var animal = await CrearAnimalAsync();
        var sesion = await IniciarSesionAsync();

        using var respuesta = await SubirCapturaAsync(sesion.Id, animal.Id, fechaCaptura: DateTimeOffset.UtcNow.AddDays(1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        var cuerpo = await respuesta.LeerAsync<JsonElement>();
        Assert.Equal("La fecha de captura no puede ser posterior al momento actual", cuerpo.GetProperty("detail").GetString());
        Assert.Empty(Almacenamiento.Archivos);
    }

    [Fact]
    public async Task Subir_una_captura_valida_la_registra_con_su_imagen_y_huella()
    {
        var animal = await CrearAnimalAsync();
        var sesion = await IniciarSesionAsync();
        var fecha = new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.FromHours(-3));

        using var respuesta = await SubirCapturaAsync(sesion.Id, animal.Id, fechaCaptura: fecha);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.LeerAsync<JsonElement>();
        var idCaptura = cuerpo.GetProperty("id").GetGuid();
        Assert.Equal(sesion.Id, cuerpo.GetProperty("id_sesion").GetGuid());
        Assert.Equal(animal.Id, cuerpo.GetProperty("id_animal").GetGuid());
        Assert.Equal(fecha, cuerpo.GetProperty("fecha_captura").GetDateTimeOffset());
        Assert.Equal("registrada", cuerpo.GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("motivo_fallo").ValueKind);
        Assert.Equal(0, cuerpo.GetProperty("intentos").GetInt32());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("fecha_revision").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("id_evento_pesaje").ValueKind);

        var guardado = Assert.Single(Almacenamiento.Archivos);
        Assert.Equal(ImagenValida, guardado.Value);
        var imagen = await EnContextoAsync(contexto => contexto.Imagenes.SingleAsync(i => i.IdCaptura == idCaptura, Cancelacion));
        Assert.Equal(guardado.Key, imagen.ReferenciaImagen);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(ImagenValida)), imagen.HuellaVerificacion);
    }

    [Fact]
    public async Task Obtener_captura_devuelve_su_estado_y_404_si_no_existe()
    {
        var animal = await CrearAnimalAsync();
        var sesion = await IniciarSesionAsync();
        var captura = await SubirCapturaValidaAsync(sesion.Id, animal.Id);

        var obtenida = await ClienteCapturas.GetFromJsonAsync<CapturaRespuesta>($"/capturas/{captura.Id}", Json.Opciones, Cancelacion);
        using var inexistente = await ClienteCapturas.GetAsync(new Uri($"/capturas/{Guid.CreateVersion7()}", UriKind.Relative), Cancelacion);

        // PostgreSQL keeps microseconds, so the stored date may lose the last tick digit.
        Assert.Equal(captura.FechaCaptura, obtenida!.FechaCaptura, TimeSpan.FromMilliseconds(1));
        Assert.Equal(captura, obtenida with { FechaCaptura = captura.FechaCaptura });
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Theory]
    [InlineData("application/pdf", 19, "Formato de imagen no soportado")]
    [InlineData("image/jpeg", 0, "El archivo está vacío")]
    public async Task Una_subida_invalida_responde_422_con_el_motivo_y_no_guarda_nada(string tipoContenido, int bytes, string motivo)
    {
        var animal = await CrearAnimalAsync();
        var sesion = await IniciarSesionAsync();

        using var respuesta = await SubirCapturaAsync(sesion.Id, animal.Id, tipoContenido, new byte[bytes]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        var problema = await respuesta.LeerAsync<ProblemDetails>();
        Assert.Equal(motivo, problema!.Detail);
        Assert.Empty(Almacenamiento.Archivos);
        Assert.Equal((0, 0), await ContarFilasAsync());
    }

    [Fact]
    public async Task Subir_con_sesion_o_animal_inexistente_responde_404()
    {
        var animal = await CrearAnimalAsync();
        var sesion = await IniciarSesionAsync();

        using var sinSesion = await SubirCapturaAsync(Guid.CreateVersion7(), animal.Id);
        using var sinAnimal = await SubirCapturaAsync(sesion.Id, Guid.CreateVersion7());

        Assert.Equal(HttpStatusCode.NotFound, sinSesion.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, sinAnimal.StatusCode);
        Assert.Empty(Almacenamiento.Archivos);
        Assert.Equal((0, 0), await ContarFilasAsync());
    }

    [Fact]
    public async Task Finalizar_sesion_impide_subir_capturas_y_no_se_puede_repetir()
    {
        var animal = await CrearAnimalAsync();
        var sesion = await IniciarSesionAsync();

        using var finalizar = await ClienteCapturas.PostAsync(new Uri($"/sesiones-captura/{sesion.Id}/finalizar", UriKind.Relative), null, Cancelacion);
        using var otraVez = await ClienteCapturas.PostAsync(new Uri($"/sesiones-captura/{sesion.Id}/finalizar", UriKind.Relative), null, Cancelacion);
        using var inexistente = await ClienteCapturas.PostAsync(new Uri($"/sesiones-captura/{Guid.CreateVersion7()}/finalizar", UriKind.Relative), null, Cancelacion);
        using var subida = await SubirCapturaAsync(sesion.Id, animal.Id);

        Assert.Equal(HttpStatusCode.OK, finalizar.StatusCode);
        var finalizada = await finalizar.LeerAsync<SesionCapturaRespuesta>();
        Assert.Equal(EstadoSesionCaptura.Finalizada, finalizada!.Estado);
        Assert.NotNull(finalizada.FechaFin);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, otraVez.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, subida.StatusCode);
        Assert.Equal((0, 0), await ContarFilasAsync());
    }

    [Fact]
    public async Task La_galeria_de_la_sesion_lista_sus_capturas_por_fecha_de_captura()
    {
        var vaca = await CrearAnimalAsync("AR001");
        var toro = await CrearAnimalAsync("AR002");
        var sesion = await IniciarSesionAsync();
        var otraSesion = await IniciarSesionAsync();
        var temprano = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        var tarde = temprano.AddHours(2);
        var segunda = await SubirCapturaValidaAsync(sesion.Id, toro.Id, tarde);
        var primera = await SubirCapturaValidaAsync(sesion.Id, vaca.Id, temprano);
        await SubirCapturaValidaAsync(otraSesion.Id, vaca.Id, temprano);

        var galeria = await ClienteCapturas.GetFromJsonAsync<JsonElement>($"/sesiones-captura/{sesion.Id}/capturas", Json.Opciones, Cancelacion);
        using var inexistente = await ClienteCapturas.GetAsync(new Uri($"/sesiones-captura/{Guid.CreateVersion7()}/capturas", UriKind.Relative), Cancelacion);

        var items = galeria.EnumerateArray().ToList();
        Assert.Equal([primera.Id, segunda.Id], items.Select(i => i.GetProperty("id").GetGuid()));
        Assert.Equal(["AR001", "AR002"], items.Select(i => i.GetProperty("animal_identificador").GetString()));
        Assert.Equal(vaca.Id, items[0].GetProperty("id_animal").GetGuid());
        Assert.Equal(temprano, items[0].GetProperty("fecha_captura").GetDateTimeOffset());
        Assert.Equal("registrada", items[0].GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("motivo_fallo").ValueKind);
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("id_evento_pesaje").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task Descartar_una_captura_registrada_responde_204_y_borra_captura_imagen_y_archivo()
    {
        var animal = await CrearAnimalAsync();
        var sesion = await IniciarSesionAsync();
        var captura = await SubirCapturaValidaAsync(sesion.Id, animal.Id);

        using var respuesta = await ClienteCapturas.DeleteAsync(new Uri($"/capturas/{captura.Id}", UriKind.Relative), Cancelacion);
        using var consulta = await ClienteCapturas.GetAsync(new Uri($"/capturas/{captura.Id}", UriKind.Relative), Cancelacion);
        using var otraVez = await ClienteCapturas.DeleteAsync(new Uri($"/capturas/{captura.Id}", UriKind.Relative), Cancelacion);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, consulta.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otraVez.StatusCode);
        Assert.Equal((0, 0), await ContarFilasAsync());
        Assert.Empty(Almacenamiento.Archivos);
    }

    [Fact]
    public async Task Descartar_una_captura_enviada_a_estimar_responde_422_y_no_borra_nada()
    {
        var animal = await CrearAnimalAsync();
        var sesion = await IniciarSesionAsync();
        var captura = await SubirCapturaValidaAsync(sesion.Id, animal.Id);
        using var pesaje = await ClienteCapturas.PostAsJsonAsync(new Uri("/pesajes", UriKind.Relative), new { id_captura = captura.Id }, Cancelacion);
        pesaje.EnsureSuccessStatusCode();

        using var respuesta = await ClienteCapturas.DeleteAsync(new Uri($"/capturas/{captura.Id}", UriKind.Relative), Cancelacion);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal((1, 1), await ContarFilasAsync());
        Assert.Single(Almacenamiento.Archivos);
    }

    [Fact]
    public async Task Descartar_una_captura_inexistente_responde_404()
    {
        using var respuesta = await ClienteCapturas.DeleteAsync(new Uri($"/capturas/{Guid.CreateVersion7()}", UriKind.Relative), Cancelacion);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
