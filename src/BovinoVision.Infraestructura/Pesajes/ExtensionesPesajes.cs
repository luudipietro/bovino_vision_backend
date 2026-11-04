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
        servicios.AgregarEstimador(configuracion.GetSection(OpcionesEstimador.Seccion));
        servicios.AddHostedService<ServicioInferencia>();
        return servicios;
    }

    private static void AgregarEstimador(this IServiceCollection servicios, IConfigurationSection seccion)
    {
        if (string.IsNullOrWhiteSpace(seccion[nameof(OpcionesEstimador.UrlBase)]))
        {
            servicios.AddSingleton<IEstimadorPeso, EstimadorPesoSimulado>();
            return;
        }

        servicios.AddOptions<OpcionesEstimador>().Bind(seccion)
            .Validate(o => Uri.TryCreate(o.UrlBase, UriKind.Absolute, out var url) && url.Scheme is "http" or "https"
                    && !string.IsNullOrWhiteSpace(o.Token) && o.IntervaloConsultaMilisegundos >= 0,
                "Estimador necesita una UrlBase http(s) absoluta, un Token y un intervalo de consulta no negativo").ValidateOnStart();
        servicios.AddHttpClient<IEstimadorPeso, EstimadorPesoHttp>((proveedor, cliente) =>
        {
            var opciones = proveedor.GetRequiredService<IOptions<OpcionesEstimador>>().Value;
            cliente.BaseAddress = new Uri($"{opciones.UrlBase!.TrimEnd('/')}/");
            cliente.DefaultRequestHeaders.Authorization = new("Bearer", opciones.Token);
        });
    }
}

/// <summary>Adaptador usado cuando no se configura el servicio de estimación; peso simulado determinístico.</summary>
internal sealed class EstimadorPesoSimulado : IEstimadorPeso
{
    public Task<ResultadoEstimacion> EstimarAsync(TrabajoInferencia trabajo, CancellationToken cancelacion = default) =>
        Task.FromResult(new ResultadoEstimacion(400m, "simulado-v1"));
}
