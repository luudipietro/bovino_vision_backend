using System.Net;
using System.Net.Http.Json;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Establecimientos;
using BovinoVision.Dominio.Trazabilidad;
using BovinoVision.Dominio.Usuarios;
using BovinoVision.Infraestructura.Persistencia;
using BovinoVision.Infraestructura.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Api.Tests.Capturas;

/// <summary>GET /imagenes/{id}: serves the stored photo of a capture to sessions of the same establishment.</summary>
public class ImagenesApiTests(FabricaApi fabrica) : PruebaDeCapturas(fabrica)
{
    private const string CorreoLimitado = "limitado@campo.com";
    private const string ContrasenaLimitada = "limitado-123";

    private static readonly byte[] ContenidoPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private async Task<(Guid IdCaptura, Guid IdImagen)> SubirFotoAsync(string tipoContenido = "image/jpeg", byte[]? contenido = null)
    {
        var animal = await CrearAnimalAsync();
        var sesion = await IniciarSesionAsync();
        using var respuesta = await SubirCapturaAsync(sesion.Id, animal.Id, tipoContenido, contenido);
        respuesta.EnsureSuccessStatusCode();
        var captura = (await respuesta.LeerAsync<CapturaRespuesta>())!;
        var imagen = await EnContextoAsync(contexto => contexto.Imagenes.SingleAsync(i => i.IdCaptura == captura.Id, Cancelacion));
        return (captura.Id, imagen.Id);
    }

    private static Uri RutaImagen(Guid idImagen) => new($"/imagenes/{idImagen}", UriKind.Relative);

    /// <summary>Client of the capture host (in-memory storage) logged in as a user of the given seeded role.</summary>
    private async Task<HttpClient> ClienteConRolAsync(Guid idRol)
    {
        await using (var alcance = Fabrica.Services.CreateAsyncScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
            var hasher = alcance.ServiceProvider.GetRequiredService<IHasherContrasenas>();
            contexto.Usuarios.Add(new Usuario(
                DatosSemilla.IdEstablecimiento, idRol, "Usuario limitado", CorreoLimitado, hasher.Generar(ContrasenaLimitada)));
            await contexto.SaveChangesAsync(Cancelacion);
        }

        using var anonimo = Fabrica.CrearClienteAnonimo();
        using var login = await anonimo.PostAsJsonAsync(
            "/auth/sesion", new { correo = CorreoLimitado, contrasena = ContrasenaLimitada }, Cancelacion);
        login.EnsureSuccessStatusCode();
        return ClienteDeCapturas(FabricaApi.LeerCookieDeSesion(login));
    }

    private HttpClient ClienteDeCapturas(string? token)
    {
        var cliente = FabricaCapturas.CreateClient();
        cliente.DefaultRequestHeaders.Remove("Cookie");
        if (token is not null)
        {
            cliente.DefaultRequestHeaders.Add("Cookie", $"{SesionHttp.Cookie}={token}");
        }

        return cliente;
    }

    // The domain error answers ProblemDetails; a missing route would answer a bare 404.
    private static async Task AssertImagenNoEncontradaAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var problema = await respuesta.LeerAsync<ProblemDetails>();
        Assert.Equal(404, problema!.Status);
        Assert.StartsWith("No existe la imagen ", problema.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Descargar_una_imagen_devuelve_sus_bytes_con_el_tipo_guardado_y_cache_privada()
    {
        var (_, idImagen) = await SubirFotoAsync("image/png", ContenidoPng);

        using var respuesta = await ClienteCapturas.GetAsync(RutaImagen(idImagen), Cancelacion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("image/png", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("private", respuesta.Headers.CacheControl?.ToString());
        Assert.Equal(ContenidoPng, await respuesta.Content.ReadAsByteArrayAsync(Cancelacion));
    }

    [Fact]
    public async Task Sin_sesion_responde_401()
    {
        var (_, idImagen) = await SubirFotoAsync();
        using var anonimo = ClienteDeCapturas(token: null);

        using var respuesta = await anonimo.GetAsync(RutaImagen(idImagen), Cancelacion);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_capturas_registrar_ni_rodeo_consultar_responde_403()
    {
        var (_, idImagen) = await SubirFotoAsync();
        // The initial administrator role manages users and roles only.
        using var administrador = await ClienteConRolAsync(DatosSemilla.IdRolAdministrador);

        using var respuesta = await administrador.GetAsync(RutaImagen(idImagen), Cancelacion);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Con_solo_capturas_registrar_la_imagen_se_descarga()
    {
        var (_, idImagen) = await SubirFotoAsync();
        var soloCaptura = new Rol(DatosSemilla.IdEstablecimiento, "Solo captura", [Permiso.CapturasRegistrar]);
        await EnContextoAsync(contexto =>
        {
            contexto.Roles.Add(soloCaptura);
            return contexto.SaveChangesAsync(Cancelacion);
        });
        using var operario = await ClienteConRolAsync(soloCaptura.Id);

        using var respuesta = await operario.GetAsync(RutaImagen(idImagen), Cancelacion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(ImagenValida, await respuesta.Content.ReadAsByteArrayAsync(Cancelacion));
    }

    [Fact]
    public async Task Con_solo_rodeo_consultar_la_imagen_se_descarga()
    {
        var (_, idImagen) = await SubirFotoAsync();
        // The initial producer role consults the herd but cannot register captures.
        using var productor = await ClienteConRolAsync(DatosSemilla.IdRolProductor);

        using var respuesta = await productor.GetAsync(RutaImagen(idImagen), Cancelacion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(ImagenValida, await respuesta.Content.ReadAsByteArrayAsync(Cancelacion));
    }

    [Fact]
    public async Task Una_imagen_inexistente_responde_404_con_problem_details()
    {
        using var respuesta = await ClienteCapturas.GetAsync(RutaImagen(Guid.CreateVersion7()), Cancelacion);

        await AssertImagenNoEncontradaAsync(respuesta);
    }

    [Fact]
    public async Task Una_imagen_cuyo_archivo_ya_no_esta_responde_404()
    {
        var (_, idImagen) = await SubirFotoAsync();
        Almacenamiento.Archivos.Clear();

        using var respuesta = await ClienteCapturas.GetAsync(RutaImagen(idImagen), Cancelacion);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        await AssertImagenNoEncontradaAsync(respuesta);
    }

    [Fact]
    public async Task Una_imagen_de_otro_establecimiento_responde_404_aunque_el_archivo_exista()
    {
        var otroEstablecimiento = Guid.CreateVersion7();
        var referencia = $"memoria/{Guid.NewGuid():N}.jpg";
        Almacenamiento.Archivos[referencia] = ImagenValida;
        var idImagen = await EnContextoAsync(async contexto =>
        {
            contexto.Establecimientos.Add(new Establecimiento(otroEstablecimiento, "Estancia ajena"));
            var animal = new Animal(otroEstablecimiento, "AJENO-1");
            var sesion = new SesionCaptura(Fabrica.IdUsuarioPruebas, DateTimeOffset.UtcNow);
            var captura = new Captura(animal.Id, Fabrica.IdUsuarioPruebas, sesion.Id, DateTimeOffset.UtcNow);
            var imagen = captura.AgregarImagen(referencia, "huella", "image/jpeg");
            contexto.AddRange(animal, sesion, captura, imagen);
            await contexto.SaveChangesAsync(Cancelacion);
            return imagen.Id;
        });

        try
        {
            using var respuesta = await ClienteCapturas.GetAsync(RutaImagen(idImagen), Cancelacion);

            await AssertImagenNoEncontradaAsync(respuesta);
        }
        finally
        {
            // The cleanup script does not empty establishments.
            await Fabrica.LimpiarBaseAsync();
            await EnContextoAsync(contexto => contexto.Establecimientos.Where(e => e.Id == otroEstablecimiento).ExecuteDeleteAsync(Cancelacion));
        }
    }
}
