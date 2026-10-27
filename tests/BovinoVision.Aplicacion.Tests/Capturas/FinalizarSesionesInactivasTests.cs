using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Dominio.Capturas;

namespace BovinoVision.Aplicacion.Tests.Capturas;

public class FinalizarSesionesInactivasTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 27, 22, 0, 0, TimeSpan.Zero);

    private readonly SesionesEnMemoria _sesiones = new();
    private readonly UnidadDeTrabajoContadora _unidad = new();
    private readonly TiempoControlado _tiempo = new(Ahora);

    private FinalizarSesionesInactivas CasoDeUso(int horasInactividad = 6) =>
        new(_sesiones, _unidad, _tiempo, new OpcionesSesionesCaptura { HorasInactividad = horasInactividad });

    private SesionCaptura Sesion(DateTimeOffset inicio, DateTimeOffset? ultimaCaptura = null)
    {
        var sesion = new SesionCaptura(Guid.CreateVersion7(), inicio);
        _sesiones.Agregar(sesion);
        if (ultimaCaptura is { } fecha) { _sesiones.UltimaCaptura[sesion.Id] = fecha; }
        return sesion;
    }

    [Fact]
    public async Task Finaliza_las_sesiones_sin_actividad_desde_el_umbral_con_la_fecha_de_su_ultima_actividad()
    {
        var sinCapturas = Sesion(Ahora.AddHours(-8));
        var conCapturas = Sesion(Ahora.AddHours(-9), ultimaCaptura: Ahora.AddHours(-7));
        var reciente = Sesion(Ahora.AddHours(-8), ultimaCaptura: Ahora.AddHours(-1));

        var finalizadas = await CasoDeUso().EjecutarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, finalizadas);
        Assert.Equal(EstadoSesionCaptura.Finalizada, sinCapturas.Estado);
        Assert.Equal(Ahora.AddHours(-8), sinCapturas.FechaFin);
        Assert.Equal(EstadoSesionCaptura.Finalizada, conCapturas.Estado);
        Assert.Equal(Ahora.AddHours(-7), conCapturas.FechaFin);
        Assert.Equal(EstadoSesionCaptura.Activa, reciente.Estado);
        Assert.Equal(Ahora.AddHours(-6), _sesiones.UltimoLimiteConsultado);
        Assert.Equal(1, _unidad.Guardados);
    }

    [Fact]
    public async Task Sin_sesiones_inactivas_no_guarda_cambios()
    {
        Sesion(Ahora.AddHours(-1));

        var finalizadas = await CasoDeUso().EjecutarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, finalizadas);
        Assert.Equal(0, _unidad.Guardados);
    }
}
