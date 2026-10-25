using BovinoVision.Aplicacion.Pesajes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BovinoVision.Infraestructura.Pesajes;

internal sealed class ServicioInferencia(IServiceScopeFactory alcances, IOptions<OpcionesInferencia> opciones, ILogger<ServicioInferencia> registro) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> ErrorProcesamiento = LoggerMessage.Define(
        LogLevel.Error, new EventId(1, "InferenciaFallida"), "Error al procesar la cola de inferencia");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // También deja tiempo a que terminen las migraciones de arranque antes del primer sondeo.
            await Task.Delay(TimeSpan.FromSeconds(opciones.Value.IntervaloSegundos), stoppingToken);
            if (!opciones.Value.ProcesarEnSegundoPlano) { continue; }
            try
            {
                await using var alcance = alcances.CreateAsyncScope();
                await alcance.ServiceProvider.GetRequiredService<ProcesarSiguienteCaptura>().EjecutarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                // Una transacción final fallida deja la concesión persistida disponible para su recuperación.
                ErrorProcesamiento(registro, error);
            }
        }
    }
}
