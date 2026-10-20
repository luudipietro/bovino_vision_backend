using BovinoVision.Aplicacion.Analitica;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Tests.Analitica;

public class ConsultasAnaliticaTests
{
    private readonly ContextoUsuarioFijo _usuario = new();
    private readonly ConsultasAnaliticaEnMemoria _consultas = new();
    private readonly CalculadoraGmd _calculadora = Prueba.Calculadora();

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private Guid NuevoAnimal()
    {
        var id = Guid.CreateVersion7();
        _consultas.Animales.Add((_usuario.IdEstablecimiento, id));
        return id;
    }

    private PesajeDeAnimal PesajeDeAnimal(Guid idAnimal, decimal pesoKg, int dias, string? lote = null)
    {
        var fecha = Prueba.Inicio.AddDays(dias);
        var pesaje = new PesajeDeAnimal(Guid.CreateVersion7(), pesoKg, fecha, fecha.AddMinutes(5), "fake-v1", Guid.CreateVersion7(), lote);
        _consultas.PesajesDeAnimal.Add((pesaje, idAnimal));
        return pesaje;
    }

    private void Pesaje(Guid idAnimal, decimal pesoKg, DateTimeOffset fecha, Guid? idLote = null, Guid? idEstablecimiento = null) =>
        _consultas.Pesajes.Add((new PesajeResumido(idAnimal, pesoKg, fecha), idEstablecimiento ?? _usuario.IdEstablecimiento, idLote));

    private AlertaRespuesta Alerta(Guid idAnimal, int prioridad, int minutos, EstadoAlerta estado = EstadoAlerta.Activa)
    {
        var alerta = new AlertaRespuesta(
            Guid.CreateVersion7(), Guid.CreateVersion7(), idAnimal, "AR001", TipoAlerta.PerdidaPeso, prioridad, -1m, null, estado,
            Prueba.Inicio.AddMinutes(minutos));
        _consultas.Alertas.Add((alerta, _usuario.IdEstablecimiento));
        return alerta;
    }

    [Fact]
    public async Task El_historial_viene_del_mas_reciente_al_mas_antiguo_con_gmd_contra_el_anterior()
    {
        var idAnimal = NuevoAnimal();
        var segundo = PesajeDeAnimal(idAnimal, 320m, 10, "L1");
        var primero = PesajeDeAnimal(idAnimal, 300m, 0);
        var mismoDia = PesajeDeAnimal(idAnimal, 322m, 10);

        var historial = await new ConsultarHistorialPesajes(_consultas, _usuario, _calculadora)
            .EjecutarAsync(new ConsultaHistorialPesajes(idAnimal), Cancelacion);

        Assert.Equal([mismoDia.Id, segundo.Id, primero.Id], historial.Select(p => p.Id));
        Assert.Null(historial[0].GmdKgDia);
        Assert.Equal(2.00m, historial[1].GmdKgDia);
        Assert.Null(historial[2].GmdKgDia);
        Assert.Equal("L1", historial[1].LoteCodigo);
        Assert.Equal(segundo.IdImagenUtilizada, historial[1].IdImagenUtilizada);
        Assert.Equal(segundo.FechaProcesamiento, historial[1].FechaProcesamiento);
    }

    [Fact]
    public async Task El_historial_de_un_animal_inexistente_es_no_encontrado()
    {
        var casoDeUso = new ConsultarHistorialPesajes(_consultas, _usuario, _calculadora);

        await Assert.ThrowsAsync<NoEncontradoError>(() =>
            casoDeUso.EjecutarAsync(new ConsultaHistorialPesajes(Guid.CreateVersion7()), Cancelacion));
    }

    [Fact]
    public async Task Las_alertas_de_un_animal_vienen_de_la_mas_reciente_a_la_mas_antigua()
    {
        var idAnimal = NuevoAnimal();
        var vieja = Alerta(idAnimal, 1, 0, EstadoAlerta.Resuelta);
        var nueva = Alerta(idAnimal, 2, 30);
        Alerta(NuevoAnimal(), 1, 10);

        var alertas = await new ListarAlertasDeAnimal(_consultas, _usuario)
            .EjecutarAsync(new ConsultaAlertasDeAnimal(idAnimal), Cancelacion);

        Assert.Equal([nueva.Id, vieja.Id], alertas.Select(a => a.Id));
    }

    [Fact]
    public async Task Las_alertas_de_un_animal_inexistente_son_no_encontrado()
    {
        await Assert.ThrowsAsync<NoEncontradoError>(() => new ListarAlertasDeAnimal(_consultas, _usuario)
            .EjecutarAsync(new ConsultaAlertasDeAnimal(Guid.CreateVersion7()), Cancelacion));
    }

    [Fact]
    public async Task Las_alertas_activas_se_ordenan_por_prioridad_y_luego_por_fecha_descendente()
    {
        var idAnimal = NuevoAnimal();
        var estancamientoViejo = Alerta(idAnimal, 2, 0);
        var perdidaVieja = Alerta(idAnimal, 1, 10);
        var perdidaNueva = Alerta(idAnimal, 1, 20);
        var estancamientoNuevo = Alerta(idAnimal, 2, 30);
        Alerta(idAnimal, 1, 40, EstadoAlerta.Resuelta);

        var alertas = await new ListarAlertasActivas(_consultas, _usuario).EjecutarAsync(Cancelacion);

        Assert.Equal(
            [perdidaNueva.Id, perdidaVieja.Id, estancamientoNuevo.Id, estancamientoViejo.Id],
            alertas.Select(a => a.Id));
    }

    [Fact]
    public async Task El_panel_promedia_el_ultimo_peso_y_la_gmd_mas_reciente_de_cada_animal()
    {
        // Port of test_panel_promedia_el_ultimo_peso_de_cada_animal.
        var animal1 = Guid.CreateVersion7();
        var animal2 = Guid.CreateVersion7();
        Pesaje(animal1, 300m, Prueba.Inicio);
        Pesaje(animal1, 320m, Prueba.Inicio.AddDays(10));
        Pesaje(animal2, 400m, Prueba.Inicio);
        Pesaje(Guid.CreateVersion7(), 999m, Prueba.Inicio, idEstablecimiento: Guid.CreateVersion7());

        var panel = await new ConsultarPanel(_consultas, _usuario, _calculadora).EjecutarAsync(new ConsultaPanel(null), Cancelacion);

        Assert.Equal(2, panel.CantidadAnimales);
        Assert.Equal(360.0m, panel.PesoPromedioKg);
        Assert.Equal(2.0m, panel.GmdPromedioKgDia);
        Assert.Equal(1, panel.AnimalesConGmd);
    }

    [Fact]
    public async Task La_evolucion_del_panel_agrupa_por_fecha_local_en_orden_cronologico()
    {
        var animal1 = Guid.CreateVersion7();
        var animal2 = Guid.CreateVersion7();
        // 01:00 UTC on Jan 11 is Jan 10 in UTC-3.
        Pesaje(animal1, 320m, new DateTimeOffset(2026, 1, 11, 1, 0, 0, TimeSpan.Zero));
        Pesaje(animal2, 410m, new DateTimeOffset(2026, 1, 10, 15, 0, 0, TimeSpan.Zero));
        Pesaje(animal1, 300m, Prueba.Inicio);
        Pesaje(animal2, 400m, Prueba.Inicio);

        var panel = await new ConsultarPanel(_consultas, _usuario, _calculadora).EjecutarAsync(new ConsultaPanel(null), Cancelacion);

        Assert.Equal(
            [new PuntoEvolucion(new DateOnly(2026, 1, 1), 350.0m), new PuntoEvolucion(new DateOnly(2026, 1, 10), 365.0m)],
            panel.EvolucionPesoPromedio);
    }

    [Fact]
    public async Task El_panel_filtra_por_lote_y_sin_pesajes_devuelve_vacio()
    {
        var idLote = Guid.CreateVersion7();
        Pesaje(Guid.CreateVersion7(), 300m, Prueba.Inicio, idLote: Guid.CreateVersion7());

        var panel = await new ConsultarPanel(_consultas, _usuario, _calculadora).EjecutarAsync(new ConsultaPanel(idLote), Cancelacion);

        Assert.Equal(idLote, _consultas.UltimoFiltro?.IdLote);
        Assert.Equal(0, panel.CantidadAnimales);
        Assert.Null(panel.PesoPromedioKg);
        Assert.Null(panel.GmdPromedioKgDia);
        Assert.Equal(0, panel.AnimalesConGmd);
        Assert.Empty(panel.EvolucionPesoPromedio);
    }

    [Fact]
    public async Task El_reporte_rechaza_un_rango_invertido()
    {
        var casoDeUso = new GenerarReporte(_consultas, _usuario, _calculadora);

        await Assert.ThrowsAsync<OperacionInvalidaError>(() => casoDeUso.EjecutarAsync(
            new ConsultaReporte(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 31), null), Cancelacion));
    }

    [Fact]
    public async Task El_reporte_incluye_los_dias_extremos_completos_en_la_zona_configurada()
    {
        var reporte = await new GenerarReporte(_consultas, _usuario, _calculadora).EjecutarAsync(
            new ConsultaReporte(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null), Cancelacion);

        Assert.Equal(new DateTimeOffset(2026, 1, 1, 3, 0, 0, TimeSpan.Zero), _consultas.UltimoFiltro?.Desde);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 0, 0, TimeSpan.Zero), _consultas.UltimoFiltro?.Hasta);
        Assert.Equal(new ReporteRespuesta(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), 0, 0, null, null), reporte);
    }

    [Fact]
    public async Task El_reporte_promedia_pesos_y_gmd_entre_pesajes_consecutivos_del_periodo()
    {
        var animal1 = Guid.CreateVersion7();
        var animal2 = Guid.CreateVersion7();
        Pesaje(animal1, 300m, Prueba.Inicio);
        Pesaje(animal1, 320m, Prueba.Inicio.AddDays(10));
        Pesaje(animal1, 350m, Prueba.Inicio.AddDays(20));
        Pesaje(animal2, 400m, Prueba.Inicio);
        Pesaje(animal2, 404m, Prueba.Inicio.AddDays(4));
        Pesaje(animal2, 500m, Prueba.Inicio.AddDays(40)); // outside the period

        var reporte = await new GenerarReporte(_consultas, _usuario, _calculadora).EjecutarAsync(
            new ConsultaReporte(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), null), Cancelacion);

        Assert.Equal(5, reporte.CantidadPesajes);
        Assert.Equal(2, reporte.CantidadAnimales);
        Assert.Equal(354.8m, reporte.PesoPromedioKg); // 1774 / 5
        Assert.Equal(2.0m, reporte.GmdPromedioKgDia); // (2 + 3 + 1) / 3
    }
}
