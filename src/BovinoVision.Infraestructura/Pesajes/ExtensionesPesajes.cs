using BovinoVision.Aplicacion.Pesajes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BovinoVision.Infraestructura.Pesajes;

public static class ExtensionesPesajes
{
    public static IServiceCollection AgregarInfraestructuraPesajes(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddOptions<OpcionesInferencia>().Bind(configuracion.GetSection("Inferencia"))
            .Validate(o => o.MaximoIntentos > 0 && o.DemoraReintentoSegundos > 0 && o.ArrendamientoSegundos > 0 && o.IntervaloSegundos > 0,
                "Los límites e intervalos de inferencia deben ser positivos").ValidateOnStart();
        servicios.AddScoped(p => p.GetRequiredService<IOptions<OpcionesInferencia>>().Value);
        servicios.AddScoped<IColaInferencia, ColaInferencia>();
        servicios.AddScoped<IConsultasPesajes, ConsultasPesajes>();
        servicios.AddSingleton<IEstimadorPeso, EstimadorPesoSimulado>();
        servicios.AddHostedService<ServicioInferencia>();
        return servicios;
    }
}

/// <summary>Adapter used until the real vision service is integrated; deterministic simulated weight.</summary>
internal sealed class EstimadorPesoSimulado : IEstimadorPeso
{
    public Task<ResultadoEstimacion> EstimarAsync(string referenciaImagen, CancellationToken cancelacion = default) =>
        Task.FromResult(new ResultadoEstimacion(400m, "simulado-v1"));
}
