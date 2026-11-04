using BovinoVision.Aplicacion.Trazabilidad;
using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Trazabilidad;

namespace BovinoVision.Aplicacion.Tests.Trazabilidad;

public class AnimalesTests
{
    private static readonly Guid IdEstablecimiento = Guid.CreateVersion7();
    private static readonly DateTimeOffset Ahora = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TrazabilidadEnMemoria _almacen = new();
    private readonly ContextoFijo _contexto = new(IdEstablecimiento);
    private readonly CancellationToken _cancelacion = TestContext.Current.CancellationToken;

    private RegistrarAnimal Registrar(DateTimeOffset? ahora = null) =>
        new(_almacen, _almacen, _almacen, _almacen, _almacen, _contexto, new RelojFijo(ahora ?? Ahora));

    private ModificarAnimal Modificar(DateTimeOffset? ahora = null) =>
        new(_almacen, _almacen, _almacen, _almacen, _almacen, _contexto, new RelojFijo(ahora ?? Ahora));

    [Fact]
    public async Task Registrar_animal_lo_guarda_y_devuelve_su_ficha()
    {
        var animal = await Registrar().EjecutarAsync(new NuevoAnimal("AR001"), _cancelacion);

        Assert.Equal("AR001", animal.Identificador);
        Assert.Equal(IdEstablecimiento, animal.IdEstablecimiento);
        Assert.Null(animal.IdLote);
        Assert.Equal(1, _almacen.Guardados);
        Assert.Equal(animal.Id, Assert.Single(_almacen.Animales).Id);
    }

    [Fact]
    public async Task No_permite_identificador_duplicado_en_el_establecimiento()
    {
        await Registrar().EjecutarAsync(new NuevoAnimal("AR001"), _cancelacion);

        await Assert.ThrowsAsync<ConflictoError>(() => Registrar().EjecutarAsync(new NuevoAnimal(" AR001 "), _cancelacion));
        Assert.Single(_almacen.Animales);
        Assert.Equal(1, _almacen.Guardados);
    }

    [Fact]
    public async Task Permite_el_mismo_identificador_en_otro_establecimiento()
    {
        _almacen.AgregarAnimalGuardado(Guid.CreateVersion7(), "AR001");

        var animal = await Registrar().EjecutarAsync(new NuevoAnimal("AR001"), _cancelacion);

        Assert.Equal(IdEstablecimiento, animal.IdEstablecimiento);
    }

    [Fact]
    public async Task No_permite_asignar_lote_inexistente()
    {
        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            Registrar().EjecutarAsync(new NuevoAnimal("AR002", IdLote: Guid.CreateVersion7()), _cancelacion));
        Assert.Empty(_almacen.Animales);
        Assert.Equal(0, _almacen.Guardados);
    }

    [Fact]
    public async Task No_permite_asignar_lote_de_otro_establecimiento()
    {
        var ajeno = _almacen.AgregarLoteGuardado(Guid.CreateVersion7(), "L1");

        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            Registrar().EjecutarAsync(new NuevoAnimal("AR002", IdLote: ajeno.Id), _cancelacion));
    }

    [Fact]
    public async Task No_permite_raza_inexistente()
    {
        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            Registrar().EjecutarAsync(new NuevoAnimal("AR002", IdRaza: Guid.CreateVersion7()), _cancelacion));
        Assert.Equal(0, _almacen.Guardados);
    }

    [Fact]
    public async Task Registrar_con_lote_abre_la_asignacion_con_el_usuario_y_la_hora_actual()
    {
        var lote = _almacen.AgregarLoteGuardado(IdEstablecimiento, "L1");
        var raza = _almacen.Razas[0];

        var animal = await Registrar().EjecutarAsync(
            new NuevoAnimal("AR002", raza.Id, Sexo.Hembra, new DateOnly(2025, 3, 1), lote.Id),
            _cancelacion);

        Assert.Equal(lote.Id, animal.IdLote);
        Assert.Equal("L1", animal.LoteCodigo);
        Assert.Equal(raza.Nombre, animal.Raza);
        Assert.Equal(Sexo.Hembra, animal.Sexo);
        Assert.Equal(new DateOnly(2025, 3, 1), animal.FechaNacimiento);
        var asignacion = Assert.Single(Assert.Single(_almacen.Animales).HistorialLotes);
        Assert.Equal(_contexto.IdUsuario, asignacion.IdUsuario);
        Assert.Equal(Ahora, asignacion.VigenteDesde);
        Assert.Null(asignacion.VigenteHasta);
        Assert.Equal(1, _almacen.Guardados);
    }

    [Fact]
    public async Task Modificar_animal_inexistente_lanza_no_encontrado()
    {
        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            Modificar().EjecutarAsync(Guid.CreateVersion7(), new CambiosAnimal(Sexo: Sexo.Macho), _cancelacion));
    }

    [Fact]
    public async Task Modificar_animal_de_otro_establecimiento_lanza_no_encontrado()
    {
        var ajeno = _almacen.AgregarAnimalGuardado(Guid.CreateVersion7(), "AR001");

        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            Modificar().EjecutarAsync(ajeno.Id, new CambiosAnimal(Sexo: Sexo.Macho), _cancelacion));
    }

    [Fact]
    public async Task Modificar_solo_cambia_los_valores_enviados()
    {
        var raza = _almacen.Razas[0];
        var creado = await Registrar().EjecutarAsync(
            new NuevoAnimal("AR001", raza.Id, Sexo.Hembra, new DateOnly(2025, 3, 1)),
            _cancelacion);

        var modificado = await Modificar().EjecutarAsync(creado.Id, new CambiosAnimal(FechaNacimiento: new DateOnly(2025, 4, 2)), _cancelacion);

        Assert.Equal(raza.Id, modificado.IdRaza);
        Assert.Equal(Sexo.Hembra, modificado.Sexo);
        Assert.Equal(new DateOnly(2025, 4, 2), modificado.FechaNacimiento);
        Assert.Equal(2, _almacen.Guardados);
    }

    [Fact]
    public async Task Modificar_con_raza_inexistente_lanza_no_encontrado()
    {
        var creado = await Registrar().EjecutarAsync(new NuevoAnimal("AR001"), _cancelacion);

        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            Modificar().EjecutarAsync(creado.Id, new CambiosAnimal(IdRaza: Guid.CreateVersion7()), _cancelacion));
        Assert.Equal(1, _almacen.Guardados);
    }

    [Fact]
    public async Task Cambiar_de_lote_cierra_la_asignacion_vigente_y_abre_otra()
    {
        var loteA = _almacen.AgregarLoteGuardado(IdEstablecimiento, "LA");
        var loteB = _almacen.AgregarLoteGuardado(IdEstablecimiento, "LB");
        var creado = await Registrar().EjecutarAsync(new NuevoAnimal("AR001", IdLote: loteA.Id), _cancelacion);
        var despues = Ahora.AddDays(30);

        var modificado = await Modificar(despues).EjecutarAsync(creado.Id, new CambiosAnimal(IdLote: loteB.Id), _cancelacion);

        Assert.Equal(loteB.Id, modificado.IdLote);
        var historial = Assert.Single(_almacen.Animales).HistorialLotes;
        Assert.Equal(2, historial.Count);
        Assert.Equal(despues, historial.Single(h => h.IdLote == loteA.Id).VigenteHasta);
        Assert.Null(historial.Single(h => h.IdLote == loteB.Id).VigenteHasta);
    }

    [Fact]
    public async Task Reasignar_el_mismo_lote_no_registra_nada()
    {
        var lote = _almacen.AgregarLoteGuardado(IdEstablecimiento, "L1");
        var creado = await Registrar().EjecutarAsync(new NuevoAnimal("AR001", IdLote: lote.Id), _cancelacion);

        await Modificar(Ahora.AddDays(1)).EjecutarAsync(creado.Id, new CambiosAnimal(IdLote: lote.Id), _cancelacion);

        var asignacion = Assert.Single(Assert.Single(_almacen.Animales).HistorialLotes);
        Assert.Null(asignacion.VigenteHasta);
    }

    [Fact]
    public async Task Cambiar_a_lote_inexistente_lanza_no_encontrado_y_conserva_el_lote()
    {
        var lote = _almacen.AgregarLoteGuardado(IdEstablecimiento, "L1");
        var creado = await Registrar().EjecutarAsync(new NuevoAnimal("AR001", IdLote: lote.Id), _cancelacion);

        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            Modificar().EjecutarAsync(creado.Id, new CambiosAnimal(IdLote: Guid.CreateVersion7()), _cancelacion));
        Assert.Equal(lote.Id, Assert.Single(_almacen.Animales).AsignacionVigente?.IdLote);
    }

    [Fact]
    public async Task Listar_animales_filtra_por_establecimiento()
    {
        await Registrar().EjecutarAsync(new NuevoAnimal("AR001"), _cancelacion);
        _almacen.AgregarAnimalGuardado(Guid.CreateVersion7(), "AR002");

        var animales = await new ListarAnimales(_almacen, _contexto).EjecutarAsync(new FiltroAnimales(), _cancelacion);

        Assert.Equal(["AR001"], animales.Select(a => a.Identificador));
    }

    [Fact]
    public async Task Ficha_de_animal_inexistente_o_ajeno_lanza_no_encontrado()
    {
        var ajeno = _almacen.AgregarAnimalGuardado(Guid.CreateVersion7(), "AR001");
        var ficha = new ObtenerFichaAnimal(_almacen, _contexto);

        await Assert.ThrowsAsync<NoEncontradoError>(() => ficha.EjecutarAsync(Guid.CreateVersion7(), _cancelacion));
        await Assert.ThrowsAsync<NoEncontradoError>(() => ficha.EjecutarAsync(ajeno.Id, _cancelacion));
    }

    [Fact]
    public async Task Historial_de_lotes_de_animal_inexistente_lanza_no_encontrado()
    {
        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            new ObtenerHistorialLotes(_almacen, _contexto).EjecutarAsync(Guid.CreateVersion7(), _cancelacion));
    }

    [Fact]
    public async Task Historial_de_lotes_devuelve_las_asignaciones_de_la_mas_reciente_a_la_mas_antigua()
    {
        var loteA = _almacen.AgregarLoteGuardado(IdEstablecimiento, "LA");
        var loteB = _almacen.AgregarLoteGuardado(IdEstablecimiento, "LB");
        var creado = await Registrar().EjecutarAsync(new NuevoAnimal("AR001", IdLote: loteA.Id), _cancelacion);
        await Modificar(Ahora.AddDays(10)).EjecutarAsync(creado.Id, new CambiosAnimal(IdLote: loteB.Id), _cancelacion);

        var historial = await new ObtenerHistorialLotes(_almacen, _contexto).EjecutarAsync(creado.Id, _cancelacion);

        Assert.Equal(["LB", "LA"], historial.Select(h => h.LoteCodigo));
    }
}
