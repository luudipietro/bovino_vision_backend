using BovinoVision.Aplicacion.Analitica;
using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Pesajes;

namespace BovinoVision.Aplicacion.Pesajes;

public sealed class ProcesarSiguienteCaptura(
    IColaInferencia cola,
    IEstimadorPeso estimador,
    IEvaluadorAlertas alertas,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider tiempo,
    OpcionesInferencia opciones)
{
    public async Task<bool> EjecutarAsync(CancellationToken cancelacion = default)
    {
        var trabajo = await cola.ReclamarAsync(tiempo.GetUtcNow(), TimeSpan.FromSeconds(opciones.ArrendamientoSegundos), opciones.MaximoIntentos, cancelacion);
        if (trabajo is null) { return false; }

        ResultadoEstimacion resultado;
        var reintentable = false;
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
            limite.CancelAfter(TimeSpan.FromSeconds(opciones.ArrendamientoSegundos));
            resultado = await estimador.EstimarAsync(trabajo.ReferenciaImagen, limite.Token).WaitAsync(limite.Token);
        }
        catch (Exception error) when (!cancelacion.IsCancellationRequested && error is HttpRequestException or TimeoutException or OperationCanceledException)
        {
            resultado = new(null, "", "El servicio de estimación no está disponible");
            reintentable = true;
        }

        await cola.ConCapturaBloqueadaAsync(trabajo.IdCaptura, async captura =>
        {
            // El control por número de intento descarta respuestas tardías de una concesión vencida o recuperada.
            if (captura.Estado != EstadoCaptura.Procesando || captura.Intentos != trabajo.Intento) { return; }
            if (resultado.PesoKg is > 0 and <= 99999.99m && !string.IsNullOrWhiteSpace(resultado.VersionModelo) && resultado.VersionModelo.Length <= 100 && resultado.MotivoFallo is null)
            {
                var historial = await cola.ResolverHistorialAsync(captura.IdAnimal, captura.FechaCaptura, cancelacion);
                var evento = EventoPesaje.Registrar(captura, captura.Imagenes.OrderBy(i => i.Id).First().Id, historial,
                    resultado.PesoKg.Value, tiempo.GetUtcNow(), resultado.VersionModelo);
                cola.Agregar(evento);
                await alertas.EvaluarAsync(evento, cancelacion);
                captura.MarcarProcesada();
            }
            else
            {
                var motivo = resultado.MotivoFallo ?? "El estimador devolvió un resultado inválido";
                motivo = motivo[..Math.Min(motivo.Length, 500)];
                if (reintentable && captura.Intentos < opciones.MaximoIntentos)
                {
                    captura.ReintentarDespues(motivo, tiempo.GetUtcNow().AddSeconds(opciones.DemoraReintentoSegundos));
                }
                else { captura.MarcarFallida(motivo); }
            }
            await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        }, cancelacion);
        return true;
    }
}
