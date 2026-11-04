using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Tests.Capturas;

public class ObtenerImagenTests
{
    private const string Referencia = "memoria/foto.png";

    private readonly ConsultasCapturasEnMemoria _consultas = new();
    private readonly AlmacenamientoEnMemoria _almacenamiento = new();
    private readonly ContextoUsuarioFijo _usuario = new();

    private ObtenerImagen CasoDeUso() => new(_consultas, _almacenamiento, _usuario);

    private Guid ImagenGuardada(Guid idEstablecimiento)
    {
        var id = Guid.CreateVersion7();
        _consultas.Imagenes[id] = (new ImagenAlmacenada(Referencia, "image/png"), idEstablecimiento);
        _almacenamiento.Archivos[Referencia] = [1, 2, 3];
        return id;
    }

    [Fact]
    public async Task Una_imagen_del_establecimiento_de_la_sesion_se_devuelve_con_su_contenido_y_tipo()
    {
        var id = ImagenGuardada(_usuario.IdEstablecimiento);

        var archivo = await CasoDeUso().EjecutarAsync(new ObtenerImagenConsulta(id), TestContext.Current.CancellationToken);

        await using var contenido = archivo.Contenido;
        using var copia = new MemoryStream();
        await contenido.CopyToAsync(copia, TestContext.Current.CancellationToken);
        Assert.Equal([1, 2, 3], copia.ToArray());
        Assert.Equal("image/png", archivo.TipoContenido);
        Assert.Equal(_usuario.IdEstablecimiento, _consultas.EstablecimientoConsultado);
    }

    [Fact]
    public async Task Una_imagen_inexistente_o_de_otro_establecimiento_es_no_encontrada()
    {
        var ajena = ImagenGuardada(Guid.CreateVersion7());

        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            CasoDeUso().EjecutarAsync(new ObtenerImagenConsulta(Guid.CreateVersion7()), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            CasoDeUso().EjecutarAsync(new ObtenerImagenConsulta(ajena), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Una_imagen_cuyo_archivo_ya_no_esta_es_no_encontrada()
    {
        var id = ImagenGuardada(_usuario.IdEstablecimiento);
        _almacenamiento.Archivos.Clear();

        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            CasoDeUso().EjecutarAsync(new ObtenerImagenConsulta(id), TestContext.Current.CancellationToken));
    }
}
