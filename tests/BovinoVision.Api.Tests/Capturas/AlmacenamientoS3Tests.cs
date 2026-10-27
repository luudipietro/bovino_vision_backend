using System.Collections.Concurrent;
using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Infraestructura.Capturas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace BovinoVision.Api.Tests.Capturas;

public sealed class AlmacenamientoS3Tests : IDisposable
{
    private static readonly Dictionary<string, string?> ConfiguracionS3 = new()
    {
        ["Almacenamiento:S3:Bucket"] = "imagenes",
        ["Almacenamiento:S3:UrlServicio"] = "https://s3.prueba",
        ["Almacenamiento:S3:Region"] = "us-east-2",
        ["Almacenamiento:S3:IdClaveAcceso"] = "id",
        ["Almacenamiento:S3:ClaveSecreta"] = "secreto",
    };

    private readonly BucketEnMemoria _bucket = new();

    public void Dispose() => _bucket.Dispose();

    private static ServiceProvider Proveedor(Dictionary<string, string?> valores)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var servicios = new ServiceCollection();
        servicios.AddSingleton<IHostEnvironment>(new HostingEnvironment { ContentRootPath = Path.GetTempPath() });
        servicios.AgregarInfraestructuraCapturas(configuracion);
        return servicios.BuildServiceProvider();
    }

    private ServiceProvider ProveedorConBucketFalso()
    {
        var proveedor = Proveedor(ConfiguracionS3);
        // El cliente se crea al resolverlo por primera vez, así que todavía toma esta fábrica de HttpClient.
        proveedor.GetRequiredService<AmazonS3Config>().HttpClientFactory = new FabricaHttpFalsa(_bucket);
        return proveedor;
    }

    [Fact]
    public void Sin_bucket_configurado_usa_el_almacenamiento_local()
    {
        using var proveedor = Proveedor([]);

        Assert.Equal("AlmacenamientoLocalImagenes", proveedor.GetRequiredService<IAlmacenamientoImagenes>().GetType().Name);
    }

    [Fact]
    public void Con_bucket_pero_sin_credenciales_falla_la_validacion_de_opciones()
    {
        using var proveedor = Proveedor(new() { ["Almacenamiento:S3:Bucket"] = "imagenes" });

        Assert.Throws<OptionsValidationException>(() => proveedor.GetRequiredService<IOptions<OpcionesAlmacenamientoS3>>().Value);
    }

    [Fact]
    public async Task Guarda_abre_y_elimina_objetos_en_el_bucket_con_direccionamiento_por_ruta()
    {
        using var proveedor = ProveedorConBucketFalso();
        var almacenamiento = proveedor.GetRequiredService<IAlmacenamientoImagenes>();
        var cancelacion = TestContext.Current.CancellationToken;
        byte[] contenido = [1, 2, 3];

        var referencia = await almacenamiento.GuardarAsync("Foto.PNG", contenido, cancelacion);
        var sinExtensionValida = await almacenamiento.GuardarAsync("../../malicioso.extension-larga", contenido, cancelacion);

        Assert.Matches("^[0-9a-f]{32}\\.png$", referencia);
        Assert.Matches("^[0-9a-f]{32}\\.jpg$", sinExtensionValida);
        Assert.Equal(contenido, _bucket.Objetos[$"/imagenes/{referencia}"]);

        await using (var abierto = await almacenamiento.AbrirAsync(referencia, cancelacion))
        {
            Assert.NotNull(abierto);
            using var copia = new MemoryStream();
            await abierto.CopyToAsync(copia, cancelacion);
            Assert.Equal(contenido, copia.ToArray());
        }

        await almacenamiento.EliminarAsync(referencia, cancelacion);
        Assert.False(_bucket.Objetos.ContainsKey($"/imagenes/{referencia}"));
        Assert.Null(await almacenamiento.AbrirAsync(referencia, cancelacion));
    }

    [Fact]
    public async Task Rechaza_referencias_que_no_son_un_nombre_de_archivo_sin_consultar_el_bucket()
    {
        using var proveedor = ProveedorConBucketFalso();
        var almacenamiento = proveedor.GetRequiredService<IAlmacenamientoImagenes>();
        var cancelacion = TestContext.Current.CancellationToken;

        Assert.Null(await almacenamiento.AbrirAsync("otra/carpeta.jpg", cancelacion));
        Assert.Null(await almacenamiento.AbrirAsync(" ", cancelacion));
        await Assert.ThrowsAsync<ArgumentException>(() => almacenamiento.EliminarAsync("../otro.jpg", cancelacion));
        Assert.Equal(0, _bucket.Pedidos);
    }

    /// <summary>Responde como un bucket S3 mínimo: PUT guarda, GET devuelve o 404, DELETE borra.</summary>
    private sealed class BucketEnMemoria : HttpMessageHandler
    {
        public ConcurrentDictionary<string, byte[]> Objetos { get; } = new();

        public int Pedidos { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken cancelacion)
        {
            Pedidos++;
            Assert.Equal("s3.prueba", pedido.RequestUri!.Host);
            var ruta = pedido.RequestUri.AbsolutePath;
            if (pedido.Method == HttpMethod.Put)
            {
                Objetos[ruta] = await pedido.Content!.ReadAsByteArrayAsync(cancelacion);
                return new HttpResponseMessage(HttpStatusCode.OK) { Headers = { ETag = new("\"etag\"") } };
            }

            if (pedido.Method == HttpMethod.Delete)
            {
                Objetos.TryRemove(ruta, out _);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return Objetos.TryGetValue(ruta, out var contenido)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(contenido) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private sealed class FabricaHttpFalsa(HttpMessageHandler manejador) : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig configuracion) => new(manejador, disposeHandler: false);
    }
}
