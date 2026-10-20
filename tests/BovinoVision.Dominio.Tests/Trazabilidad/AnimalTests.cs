using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Trazabilidad;

namespace BovinoVision.Dominio.Tests.Trazabilidad;

public class AnimalTests
{
    private static readonly Guid IdEstablecimiento = Guid.CreateVersion7();
    private static readonly Guid IdUsuario = Guid.CreateVersion7();
    private static readonly DateTimeOffset Enero = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Marzo = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void No_permite_identificador_vacio()
    {
        Assert.Throws<OperacionInvalidaError>(() => new Animal(IdEstablecimiento, "  "));
    }

    [Fact]
    public void Asignar_lote_por_primera_vez_abre_una_asignacion_vigente()
    {
        var animal = new Animal(IdEstablecimiento, "AR001");
        var idLote = Guid.CreateVersion7();

        var cambio = animal.AsignarLote(idLote, IdUsuario, Enero);

        Assert.True(cambio);
        var vigente = Assert.Single(animal.HistorialLotes);
        Assert.Equal(idLote, vigente.IdLote);
        Assert.Equal(animal.Id, vigente.IdAnimal);
        Assert.Null(vigente.VigenteHasta);
    }

    [Fact]
    public void Cambiar_de_lote_cierra_la_asignacion_anterior_y_conserva_el_historial()
    {
        var animal = new Animal(IdEstablecimiento, "AR001");
        var loteA = Guid.CreateVersion7();
        var loteB = Guid.CreateVersion7();
        animal.AsignarLote(loteA, IdUsuario, Enero);

        animal.AsignarLote(loteB, IdUsuario, Marzo);

        Assert.Equal(2, animal.HistorialLotes.Count);
        var anterior = animal.HistorialLotes.Single(h => h.IdLote == loteA);
        Assert.Equal(Marzo, anterior.VigenteHasta);
        Assert.Equal(loteB, animal.AsignacionVigente!.IdLote);
    }

    [Fact]
    public void Asignar_el_mismo_lote_no_registra_cambios()
    {
        var animal = new Animal(IdEstablecimiento, "AR001");
        var idLote = Guid.CreateVersion7();
        animal.AsignarLote(idLote, IdUsuario, Enero);

        var cambio = animal.AsignarLote(idLote, IdUsuario, Marzo);

        Assert.False(cambio);
        Assert.Single(animal.HistorialLotes);
    }

    [Fact]
    public void La_asignacion_en_una_fecha_devuelve_el_lote_de_ese_momento()
    {
        var animal = new Animal(IdEstablecimiento, "AR001");
        var loteA = Guid.CreateVersion7();
        var loteB = Guid.CreateVersion7();
        animal.AsignarLote(loteA, IdUsuario, Enero);
        animal.AsignarLote(loteB, IdUsuario, Marzo);

        Assert.Equal(loteA, animal.AsignacionEn(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero))!.IdLote);
        Assert.Equal(loteB, animal.AsignacionEn(Marzo)!.IdLote);
        Assert.Null(animal.AsignacionEn(new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Modificar_datos_solo_cambia_los_valores_informados()
    {
        var idRaza = Guid.CreateVersion7();
        var animal = new Animal(IdEstablecimiento, "AR001", idRaza, Sexo.Hembra, new DateOnly(2024, 5, 1));

        animal.ModificarDatos(idRaza: null, sexo: Sexo.Macho, fechaNacimiento: null);

        Assert.Equal(idRaza, animal.IdRaza);
        Assert.Equal(Sexo.Macho, animal.Sexo);
        Assert.Equal(new DateOnly(2024, 5, 1), animal.FechaNacimiento);
    }

    [Fact]
    public void Lote_rechaza_gmd_objetivo_no_positiva()
    {
        Assert.Throws<OperacionInvalidaError>(() => new Lote(IdEstablecimiento, "L1", gmdObjetivo: 0m));
    }
}
