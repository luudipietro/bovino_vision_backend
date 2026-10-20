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
            // Also leaves startup migrations time to complete before the first poll.
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
                // A failed final transaction leaves the persisted lease available for recovery.
                ErrorProcesamiento(registro, error);
            }
        }
    }
}
