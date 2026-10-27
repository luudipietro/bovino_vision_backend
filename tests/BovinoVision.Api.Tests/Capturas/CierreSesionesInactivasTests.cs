using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Api.Tests.Pesajes;
using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Dominio.Capturas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BovinoVision.Api.Tests.Capturas;

/// <summary>Cierre automático de las sesiones de captura que el cliente no finalizó, contra PostgreSQL real.</summary>
public class CierreSesionesInactivasTests(FabricaApi fabrica) : PruebaDeCapturas(fabrica)
{
    private readonly Reloj _reloj = new();

    protected override void ConfigurarServicios(IServiceCollection servicios)
    {
        base.ConfigurarServicios(servicios);
        servicios.RemoveAll<TimeProvider>();
        servicios.AddSingleton<TimeProvider>(_reloj);
    }

    private async Task<int> FinalizarInactivasAsync()
    {
        await using var alcance = FabricaCapturas.Services.CreateAsyncScope();
        return await alcance.ServiceProvider.GetRequiredService<FinalizarSesionesInactivas>().EjecutarAsync(Cancelacion);
    }

    private Task<SesionCaptura> LeerSesionAsync(Guid id) =>
        EnContextoAsync(contexto => contexto.SesionesCaptura.AsNoTracking().SingleAsync(s => s.Id == id, Cancelacion));

    [Fact]
    public async Task Finaliza_las_sesiones_sin_actividad_por_seis_horas_con_la_fecha_de_su_ultima_actividad()
    {
        var inicio = _reloj.Ahora;
        var animal = await CrearAnimalAsync();
        var sinCapturas = await IniciarSesionAsync();
        var conCapturaAtrasada = await IniciarSesionAsync();
        // La fecha de captura la informa el cliente: una anterior al inicio no adelanta la actividad de la sesión.
        await SubirCapturaValidaAsync(conCapturaAtrasada.Id, animal.Id, inicio.AddDays(-10));
        var finalizadaPorElCliente = await IniciarSesionAsync();
        await ClienteCapturas.PostAsync(new Uri($"/sesiones-captura/{finalizadaPorElCliente.Id}/finalizar", UriKind.Relative), null, Cancelacion);
        var conActividadReciente = await IniciarSesionAsync();
        _reloj.Ahora = inicio.AddHours(5);
        await SubirCapturaValidaAsync(conActividadReciente.Id, animal.Id);

        _reloj.Ahora = inicio.AddHours(7);
        Assert.Equal(2, await FinalizarInactivasAsync());

        foreach (var id in new[] { sinCapturas.Id, conCapturaAtrasada.Id })
        {
            var sesion = await LeerSesionAsync(id);
            Assert.Equal(EstadoSesionCaptura.Finalizada, sesion.Estado);
            Assert.Equal(inicio, sesion.FechaFin);
        }

        Assert.Equal(inicio, (await LeerSesionAsync(finalizadaPorElCliente.Id)).FechaFin);
        Assert.Equal(EstadoSesionCaptura.Activa, (await LeerSesionAsync(conActividadReciente.Id)).Estado);

        _reloj.Ahora = inicio.AddHours(12);
        Assert.Equal(1, await FinalizarInactivasAsync());
        var reciente = await LeerSesionAsync(conActividadReciente.Id);
        Assert.Equal(EstadoSesionCaptura.Finalizada, reciente.Estado);
        Assert.Equal(inicio.AddHours(5), reciente.FechaFin);
    }
}
