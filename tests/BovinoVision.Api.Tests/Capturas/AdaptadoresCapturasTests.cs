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
    public async Task El_almacenamiento_local_abre_el_contenido_guardado_y_devuelve_null_si_el_archivo_no_existe()
    {
        using var proveedor = Proveedor(new() { ["Almacenamiento:DirectorioImagenes"] = "imagenes" });
        var almacenamiento = proveedor.GetRequiredService<IAlmacenamientoImagenes>();
        var cancelacion = TestContext.Current.CancellationToken;
        byte[] contenido = [1, 2, 3];
        var referencia = await almacenamiento.GuardarAsync("foto.jpg", contenido, cancelacion);

        await using (var abierto = await almacenamiento.AbrirAsync(referencia, cancelacion))
        {
            Assert.NotNull(abierto);
            using var copia = new MemoryStream();
            await abierto.CopyToAsync(copia, cancelacion);
            Assert.Equal(contenido, copia.ToArray());
        }

        Assert.Null(await almacenamiento.AbrirAsync("00000000000000000000000000000000.jpg", cancelacion));
        await almacenamiento.EliminarAsync(referencia, cancelacion);
        Assert.Null(await almacenamiento.AbrirAsync(referencia, cancelacion));
    }

    [Fact]
    public async Task El_almacenamiento_local_elimina_una_imagen_mientras_se_esta_descargando()
    {
        using var proveedor = Proveedor(new() { ["Almacenamiento:DirectorioImagenes"] = "imagenes" });
        var almacenamiento = proveedor.GetRequiredService<IAlmacenamientoImagenes>();
        var cancelacion = TestContext.Current.CancellationToken;
        byte[] contenido = [1, 2, 3];
        var referencia = await almacenamiento.GuardarAsync("foto.jpg", contenido, cancelacion);

        await using (var abierto = await almacenamiento.AbrirAsync(referencia, cancelacion))
        {
            // Discarding a capture must not fail because a thumbnail download holds the file.
            await almacenamiento.EliminarAsync(referencia, cancelacion);

            using var copia = new MemoryStream();
            await abierto!.CopyToAsync(copia, cancelacion);
            Assert.Equal(contenido, copia.ToArray());
        }

        Assert.False(File.Exists(Path.Combine(_raiz.FullName, "imagenes", referencia)));
    }

    [Theory]
    [InlineData("../secreto.txt")]
    [InlineData("..\\secreto.txt")]
    [InlineData("subcarpeta/../../secreto.txt")]
    [InlineData("subcarpeta\\..\\..\\secreto.txt")]
    [InlineData("../imagenes-otro/dato.jpg")]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task El_almacenamiento_local_no_abre_referencias_que_salen_de_su_directorio(string referencia)
    {
        using var proveedor = Proveedor(new() { ["Almacenamiento:DirectorioImagenes"] = "imagenes" });
        var almacenamiento = proveedor.GetRequiredService<IAlmacenamientoImagenes>();
        var cancelacion = TestContext.Current.CancellationToken;
        // Files that exist next to the storage directory, one of them in a sibling whose name starts with it.
        Directory.CreateDirectory(Path.Combine(_raiz.FullName, "imagenes", "subcarpeta"));
        Directory.CreateDirectory(Path.Combine(_raiz.FullName, "imagenes-otro"));
        await File.WriteAllBytesAsync(Path.Combine(_raiz.FullName, "secreto.txt"), [9], cancelacion);
        await File.WriteAllBytesAsync(Path.Combine(_raiz.FullName, "imagenes-otro", "dato.jpg"), [9], cancelacion);

        Assert.Null(await almacenamiento.AbrirAsync(referencia, cancelacion));
    }

    [Fact]
    public async Task El_almacenamiento_local_no_abre_una_ruta_absoluta_aunque_el_archivo_exista()
    {
        using var proveedor = Proveedor(new() { ["Almacenamiento:DirectorioImagenes"] = "imagenes" });
        var almacenamiento = proveedor.GetRequiredService<IAlmacenamientoImagenes>();
        var cancelacion = TestContext.Current.CancellationToken;
        var fuera = Path.Combine(_raiz.FullName, "secreto.txt");
        await File.WriteAllBytesAsync(fuera, [9], cancelacion);

        Assert.Null(await almacenamiento.AbrirAsync(fuera, cancelacion));
    }

    [Fact]
    public void El_directorio_de_imagenes_por_defecto_es_uploads()
    {
        Assert.Equal("uploads", new OpcionesAlmacenamiento().DirectorioImagenes);
    }
}
