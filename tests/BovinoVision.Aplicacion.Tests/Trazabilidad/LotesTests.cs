using BovinoVision.Aplicacion.Trazabilidad;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Tests.Trazabilidad;

public class LotesTests
{
    private static readonly Guid IdEstablecimiento = Guid.CreateVersion7();

    private readonly TrazabilidadEnMemoria _almacen = new();
    private readonly ContextoFijo _contexto = new(IdEstablecimiento);
    private readonly CancellationToken _cancelacion = TestContext.Current.CancellationToken;

    private CrearLote Crear() => new(_almacen, _almacen, _almacen, _contexto);

    private ModificarLote Modificar() => new(_almacen, _almacen, _almacen, _contexto);

    [Fact]
    public async Task Crear_lote_lo_guarda_en_el_establecimiento_actual()
    {
        var lote = await Crear().EjecutarAsync(new NuevoLote("L1", "Recría", 0.8m), _cancelacion);

        Assert.Equal("L1", lote.Codigo);
        Assert.Equal("Recría", lote.Nombre);
        Assert.Equal(0.8m, lote.GmdObjetivo);
        Assert.Equal(0, lote.CantidadAnimales);
        Assert.Equal(IdEstablecimiento, Assert.Single(_almacen.Lotes).IdEstablecimiento);
        Assert.Equal(1, _almacen.Guardados);
    }

    [Fact]
    public async Task No_permite_codigo_duplicado_en_el_establecimiento()
    {
        await Crear().EjecutarAsync(new NuevoLote("L1"), _cancelacion);

        await Assert.ThrowsAsync<ConflictoError>(() => Crear().EjecutarAsync(new NuevoLote(" L1"), _cancelacion));
        Assert.Single(_almacen.Lotes);
    }

    [Fact]
    public async Task Permite_el_mismo_codigo_en_otro_establecimiento()
    {
        _almacen.AgregarLoteGuardado(Guid.CreateVersion7(), "L1");

        var lote = await Crear().EjecutarAsync(new NuevoLote("L1"), _cancelacion);

        Assert.Equal("L1", lote.Codigo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public async Task No_permite_gmd_objetivo_no_positiva(double gmd)
    {
        await Assert.ThrowsAsync<OperacionInvalidaError>(() =>
            Crear().EjecutarAsync(new NuevoLote("L1", GmdObjetivo: (decimal)gmd), _cancelacion));
        Assert.Equal(0, _almacen.Guardados);
    }

    [Fact]
    public async Task Modificar_lote_solo_cambia_los_valores_enviados()
    {
        var lote = _almacen.AgregarLoteGuardado(IdEstablecimiento, "L1", 0.8m);

        var renombrado = await Modificar().EjecutarAsync(lote.Id, new CambiosLote(Nombre: "Engorde"), _cancelacion);

        Assert.Equal("Engorde", renombrado.Nombre);
        Assert.Equal(0.8m, renombrado.GmdObjetivo);

        var conNuevaGmd = await Modificar().EjecutarAsync(lote.Id, new CambiosLote(GmdObjetivo: 1.1m), _cancelacion);

        Assert.Equal("Engorde", conNuevaGmd.Nombre);
        Assert.Equal(1.1m, conNuevaGmd.GmdObjetivo);
        Assert.Equal(2, _almacen.Guardados);
    }

    [Fact]
    public async Task Modificar_lote_inexistente_o_ajeno_lanza_no_encontrado()
    {
        var ajeno = _almacen.AgregarLoteGuardado(Guid.CreateVersion7(), "L1");

        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            Modificar().EjecutarAsync(Guid.CreateVersion7(), new CambiosLote(Nombre: "X"), _cancelacion));
        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            Modificar().EjecutarAsync(ajeno.Id, new CambiosLote(Nombre: "X"), _cancelacion));
    }

    [Fact]
    public async Task Listar_lotes_cuenta_los_animales_con_asignacion_vigente()
    {
        var lote = _almacen.AgregarLoteGuardado(IdEstablecimiento, "L2");
        _almacen.AgregarLoteGuardado(IdEstablecimiento, "L1");
        _almacen.AgregarLoteGuardado(Guid.CreateVersion7(), "L0");
        var animal = _almacen.AgregarAnimalGuardado(IdEstablecimiento, "AR001");
        animal.AsignarLote(lote.Id, _contexto.IdUsuario, DateTimeOffset.UtcNow);

        var lotes = await new ListarLotes(_almacen, _contexto).EjecutarAsync(_cancelacion);

        Assert.Equal(["L1", "L2"], lotes.Select(l => l.Codigo));
        Assert.Equal(1, lotes.Single(l => l.Codigo == "L2").CantidadAnimales);
    }

    [Fact]
    public async Task Listar_razas_devuelve_el_catalogo_ordenado_por_nombre()
    {
        var razas = await new ListarRazas(_almacen).EjecutarAsync(_cancelacion);

        Assert.Equal(["Aberdeen Angus", "Hereford"], razas.Select(r => r.Nombre));
    }
}
