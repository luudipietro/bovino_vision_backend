using BovinoVision.Aplicacion.Analitica;
using BovinoVision.Dominio.Analitica;

namespace BovinoVision.Aplicacion.Tests.Analitica;

public class EvaluadorAlertasTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 2, 1, 15, 0, 0, TimeSpan.Zero);

    private readonly Guid _idAnimal = Guid.CreateVersion7();
    private readonly RepositorioAlertasEnMemoria _repositorio = new();
    private readonly EvaluadorAlertas _evaluador;

    public EvaluadorAlertasTests()
    {
        _evaluador = new EvaluadorAlertas(
            _repositorio, Prueba.Calculadora(), new ReglasAlerta(new ParametrosReglasAlerta()), new RelojFijo(Ahora));
    }

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task El_primer_pesaje_no_genera_alerta()
    {
        var evento = Prueba.Evento(_idAnimal, 300m, Prueba.Inicio);

        var alerta = await _evaluador.EvaluarAsync(evento, Cancelacion);

        Assert.Null(alerta);
        Assert.Empty(_repositorio.Preparadas);
    }

    [Fact]
    public async Task Una_perdida_de_peso_prepara_una_alerta_sin_guardarla()
    {
        _repositorio.Eventos.Add(Prueba.Evento(_idAnimal, 320m, Prueba.Inicio));
        var evento = Prueba.Evento(_idAnimal, 300m, Prueba.Inicio.AddDays(10));

        var alerta = await _evaluador.EvaluarAsync(evento, Cancelacion);

        Assert.NotNull(alerta);
        Assert.Same(alerta, Assert.Single(_repositorio.Preparadas));
        Assert.Empty(_repositorio.Guardadas);
        Assert.Equal(evento.Id, alerta.IdEvento);
        Assert.Equal(TipoAlerta.PerdidaPeso, alerta.Tipo);
        Assert.Equal(1, alerta.Prioridad);
        Assert.Equal(-2.00m, alerta.Variacion);
        Assert.Equal(Ahora, alerta.FechaGeneracion);
    }

    [Fact]
    public async Task Compara_con_el_pesaje_inmediatamente_anterior_por_fecha_de_pesaje()
    {
        // Registrados fuera de orden: la base debe ser el pesaje posterior, no el último insertado.
        _repositorio.Eventos.Add(Prueba.Evento(_idAnimal, 310m, Prueba.Inicio.AddDays(5)));
        _repositorio.Eventos.Add(Prueba.Evento(_idAnimal, 400m, Prueba.Inicio));
        _repositorio.Eventos.Add(Prueba.Evento(_idAnimal, 250m, Prueba.Inicio.AddDays(30)));
        _repositorio.Eventos.Add(Prueba.Evento(Guid.CreateVersion7(), 500m, Prueba.Inicio.AddDays(8)));
        var evento = Prueba.Evento(_idAnimal, 320m, Prueba.Inicio.AddDays(10));

        var alerta = await _evaluador.EvaluarAsync(evento, Cancelacion);

        Assert.Null(alerta); // +2 kg/day against 310 kg five days before
    }

    [Fact]
    public async Task Un_aumento_bajo_el_objetivo_del_lote_historico_genera_estancamiento()
    {
        var idHistorial = Guid.CreateVersion7();
        _repositorio.GmdObjetivoPorHistorial[idHistorial] = 1.0m;
        _repositorio.Eventos.Add(Prueba.Evento(_idAnimal, 300m, Prueba.Inicio));
        var evento = Prueba.Evento(_idAnimal, 303m, Prueba.Inicio.AddDays(10), idHistorial);

        var alerta = await _evaluador.EvaluarAsync(evento, Cancelacion);

        Assert.NotNull(alerta);
        Assert.Equal(TipoAlerta.Estancamiento, alerta.Tipo);
        Assert.Equal(2, alerta.Prioridad);
        Assert.Equal(0.30m, alerta.Variacion);
        Assert.Equal(1.0m, alerta.GmdEsperada);
    }

    [Fact]
    public async Task Sin_lote_un_aumento_bajo_no_genera_alerta()
    {
        _repositorio.Eventos.Add(Prueba.Evento(_idAnimal, 300m, Prueba.Inicio));
        var evento = Prueba.Evento(_idAnimal, 300.5m, Prueba.Inicio.AddDays(10));

        Assert.Null(await _evaluador.EvaluarAsync(evento, Cancelacion));
    }

    [Fact]
    public async Task Un_pesaje_del_mismo_dia_no_tiene_gmd_y_no_genera_alerta()
    {
        _repositorio.Eventos.Add(Prueba.Evento(_idAnimal, 320m, Prueba.Inicio));
        var evento = Prueba.Evento(_idAnimal, 250m, Prueba.Inicio.AddHours(3));

        Assert.Null(await _evaluador.EvaluarAsync(evento, Cancelacion));
    }

    [Fact]
    public async Task Evaluar_dos_veces_el_mismo_evento_no_duplica_la_alerta()
    {
        _repositorio.Eventos.Add(Prueba.Evento(_idAnimal, 320m, Prueba.Inicio));
        var evento = Prueba.Evento(_idAnimal, 300m, Prueba.Inicio.AddDays(10));

        var primera = await _evaluador.EvaluarAsync(evento, Cancelacion);
        var segunda = await _evaluador.EvaluarAsync(evento, Cancelacion);

        Assert.NotNull(primera);
        Assert.Null(segunda);
        Assert.Single(_repositorio.Preparadas);
    }
}
