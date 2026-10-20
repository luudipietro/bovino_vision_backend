using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Infraestructura.Capturas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace BovinoVision.Api.Tests.Capturas;

public sealed class AdaptadoresCapturasTests : IDisposable
{
    private readonly DirectoryInfo _raiz = Directory.CreateTempSubdirectory("bovino-capturas-");

    public void Dispose() => _raiz.Delete(recursive: true);

    private ServiceProvider Proveedor(Dictionary<string, string?> valores)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var servicios = new ServiceCollection();
        servicios.AddSingleton<IHostEnvironment>(new HostingEnvironment { ContentRootPath = _raiz.FullName });
        servicios.AgregarInfraestructuraCapturas(configuracion);
        return servicios.BuildServiceProvider();
    }

    [Fact]
    public async Task El_almacenamiento_local_guarda_con_nombre_guid_en_el_directorio_configurado_y_elimina()
    {
        using var proveedor = Proveedor(new() { ["Almacenamiento:DirectorioImagenes"] = "imagenes" });
        var almacenamiento = proveedor.GetRequiredService<IAlmacenamientoImagenes>();
        var cancelacion = TestContext.Current.CancellationToken;
        byte[] contenido = [1, 2, 3];

        var referencia = await almacenamiento.GuardarAsync("Foto.PNG", contenido, cancelacion);
        var sinExtensionValida = await almacenamiento.GuardarAsync("../../malicioso.extension-larga", contenido, cancelacion);

        Assert.Matches("^[0-9a-f]{32}\\.png$", referencia);
        Assert.Matches("^[0-9a-f]{32}\\.jpg$", sinExtensionValida);
        var ruta = Path.Combine(_raiz.FullName, "imagenes", referencia);
        Assert.Equal(contenido, await File.ReadAllBytesAsync(ruta, cancelacion));

        await almacenamiento.EliminarAsync(referencia, cancelacion);
        Assert.False(File.Exists(ruta));
        await Assert.ThrowsAsync<ArgumentException>(() => almacenamiento.EliminarAsync("../otro.jpg", cancelacion));
    }

    [Fact]
    public void El_directorio_de_imagenes_por_defecto_es_uploads()
    {
        Assert.Equal("uploads", new OpcionesAlmacenamiento().DirectorioImagenes);
    }
}
