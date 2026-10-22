using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Dominio.Trazabilidad;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BovinoVision.Api.Tests.Capturas;

/// <summary>
/// Integration tests of the Capturas module: a host with in-memory image storage, so they
/// never write files. Animals and lots are inserted directly because Trazabilidad owns their endpoints.
/// </summary>
public abstract class PruebaDeCapturas(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    protected static readonly byte[] ImagenValida = "contenido-de-prueba"u8.ToArray();

    private WebApplicationFactory<Program>? _fabricaCapturas;

    private HttpClient? _cliente;

    protected WebApplicationFactory<Program> FabricaCapturas
    {
        get
        {
            return _fabricaCapturas ??= Fabrica.WithWebHostBuilder(ConfigurarHost);
        }
    }

    protected HttpClient ClienteCapturas => _cliente ??= FabricaCapturas.CreateClient();

    protected AlmacenamientoImagenesEnMemoria Almacenamiento =>
        FabricaCapturas.Services.GetRequiredService<AlmacenamientoImagenesEnMemoria>();

    protected static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Almacenamiento.Archivos.Clear();
    }

    public override async ValueTask DisposeAsync()
    {
        _cliente?.Dispose();
        if (_fabricaCapturas is not null) { await _fabricaCapturas.DisposeAsync(); }
        GC.SuppressFinalize(this);
        await base.DisposeAsync();
    }

    protected virtual void ConfigurarServicios(IServiceCollection servicios)
    {
        servicios.AddSingleton<AlmacenamientoImagenesEnMemoria>();
        servicios.RemoveAll<IAlmacenamientoImagenes>();
        servicios.AddSingleton<IAlmacenamientoImagenes>(proveedor => proveedor.GetRequiredService<AlmacenamientoImagenesEnMemoria>());
    }

    protected async Task<T> EnContextoAsync<T>(Func<BovinoVisionDbContext, Task<T>> accion)
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        return await accion(alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>());
    }

    protected Task<Animal> CrearAnimalAsync(string identificador = "AR001") =>
        EnContextoAsync(async contexto =>
        {
            var animal = new Animal(DatosSemilla.IdEstablecimiento, identificador);
            contexto.Animales.Add(animal);
            await contexto.SaveChangesAsync(Cancelacion);
            return animal;
        });

    protected async Task<SesionCapturaRespuesta> IniciarSesionAsync()
    {
        using var respuesta = await ClienteCapturas.PostAsync(new Uri("/sesiones-captura", UriKind.Relative), null, Cancelacion);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerAsync<SesionCapturaRespuesta>())!;
    }

    protected async Task<HttpResponseMessage> SubirCapturaAsync(
        Guid idSesion,
        Guid idAnimal,
        string tipoContenido = "image/jpeg",
        byte[]? contenido = null,
        DateTimeOffset? fechaCaptura = null)
    {
        using var formulario = new MultipartFormDataContent
        {
            { new StringContent(idSesion.ToString()), "id_sesion" },
            { new StringContent(idAnimal.ToString()), "id_animal" },
        };
        if (fechaCaptura is { } fecha)
        {
            formulario.Add(new StringContent(fecha.ToString("O", CultureInfo.InvariantCulture)), "fecha_captura");
        }

        var archivo = new ByteArrayContent(contenido ?? ImagenValida);
        archivo.Headers.ContentType = new MediaTypeHeaderValue(tipoContenido);
        formulario.Add(archivo, "imagen", "foto.jpg");

        return await ClienteCapturas.PostAsync(new Uri("/capturas", UriKind.Relative), formulario, Cancelacion);
    }

    protected async Task<CapturaRespuesta> SubirCapturaValidaAsync(Guid idSesion, Guid idAnimal, DateTimeOffset? fechaCaptura = null)
    {
        using var respuesta = await SubirCapturaAsync(idSesion, idAnimal, fechaCaptura: fechaCaptura);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerAsync<CapturaRespuesta>())!;
    }

    protected Task<(int Capturas, int Imagenes)> ContarFilasAsync() =>
        EnContextoAsync(async contexto =>
            (await contexto.Capturas.CountAsync(Cancelacion), await contexto.Imagenes.CountAsync(Cancelacion)));

    private void ConfigurarHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Inferencia:ProcesarEnSegundoPlano", "false");
        builder.ConfigureTestServices(ConfigurarServicios);
    }
}

public sealed class AlmacenamientoImagenesEnMemoria : IAlmacenamientoImagenes
{
    public ConcurrentDictionary<string, byte[]> Archivos { get; } = new();

    public Task<string> GuardarAsync(string nombreSugerido, ReadOnlyMemory<byte> contenido, CancellationToken cancelacion = default)
    {
        var referencia = $"memoria/{Guid.NewGuid():N}{Path.GetExtension(nombreSugerido)}";
        Archivos[referencia] = contenido.ToArray();
        return Task.FromResult(referencia);
    }

    public Task<Stream?> AbrirAsync(string referencia, CancellationToken cancelacion = default) =>
        Task.FromResult<Stream?>(Archivos.TryGetValue(referencia, out var contenido) ? new MemoryStream(contenido) : null);

    public Task EliminarAsync(string referencia, CancellationToken cancelacion = default)
    {
        Archivos.TryRemove(referencia, out _);
        return Task.CompletedTask;
    }
}
