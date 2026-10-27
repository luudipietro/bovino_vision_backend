using BovinoVision.Aplicacion.Capturas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BovinoVision.Infraestructura.Capturas;

/// <summary>Finaliza periódicamente las sesiones de captura abandonadas, porque el cliente no siempre llega a cerrarlas.</summary>
internal sealed class ServicioCierreSesiones(
    IServiceScopeFactory alcances, IOptions<OpcionesSesionesCaptura> opciones, ILogger<ServicioCierreSesiones> registro) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> SesionesFinalizadas = LoggerMessage.Define<int>(
        LogLevel.Information, new EventId(1, "SesionesInactivasFinalizadas"), "Se finalizaron {Cantidad} sesiones de captura inactivas");

    private static readonly Action<ILogger, Exception?> ErrorCierre = LoggerMessage.Define(
        LogLevel.Error, new EventId(2, "CierreSesionesFallido"), "Error al finalizar las sesiones de captura inactivas");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // La espera inicial también deja terminar las migraciones de arranque.
            await Task.Delay(TimeSpan.FromMinutes(opciones.Value.IntervaloMinutos), stoppingToken);
            if (!opciones.Value.FinalizarInactivasEnSegundoPlano) { continue; }
            try
            {
                await using var alcance = alcances.CreateAsyncScope();
                var cantidad = await alcance.ServiceProvider.GetRequiredService<FinalizarSesionesInactivas>().EjecutarAsync(stoppingToken);
                if (cantidad > 0) { SesionesFinalizadas(registro, cantidad, null); }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                // Si falla, las sesiones siguen activas y se reintenta en la próxima vuelta.
                ErrorCierre(registro, error);
            }
        }
    }
}
