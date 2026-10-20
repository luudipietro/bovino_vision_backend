using BovinoVision.Aplicacion.Pesajes;
using BovinoVision.Infraestructura.Pesajes;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BovinoVision.Api.Modulos.Pesajes;

/// <summary>Persistent asynchronous inference queue and immutable weighing results (CU-14).</summary>
internal sealed class ModuloPesajes : IModulo
{
    public void AgregarServicios(IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddScoped<SolicitarPesaje>();
        servicios.AddScoped<ProcesarSiguienteCaptura>();
        servicios.AddScoped<ObtenerPesaje>();
        servicios.AgregarInfraestructuraPesajes(configuracion);
    }

    public void MapearEndpoints(IEndpointRouteBuilder rutas)
    {
        var pesajes = rutas.MapGroup("/pesajes").WithTags("pesajes");
        pesajes.MapPost("", SolicitarAsync);
        pesajes.MapGet("/{id:guid}", ObtenerAsync);
    }

    private static async Task<Accepted> SolicitarAsync(SolicitarPesajeComando comando, SolicitarPesaje casoDeUso, CancellationToken cancelacion)
    {
        await casoDeUso.EjecutarAsync(comando, cancelacion);
        return TypedResults.Accepted($"/capturas/{comando.IdCaptura}");
    }

    private static async Task<Ok<PesajeRespuesta>> ObtenerAsync(Guid id, ObtenerPesaje casoDeUso, CancellationToken cancelacion) =>
        TypedResults.Ok(await casoDeUso.EjecutarAsync(id, cancelacion));
}
