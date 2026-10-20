using BovinoVision.Aplicacion.Trazabilidad;
using BovinoVision.Infraestructura.Trazabilidad;
using Microsoft.AspNetCore.Mvc;

namespace BovinoVision.Api.Modulos.Trazabilidad;

/// <summary>Breeds catalog, lots, animals and their lot history.</summary>
public sealed class ModuloTrazabilidad : IModulo
{
    public void AgregarServicios(IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AgregarPersistenciaTrazabilidad();
        servicios.AddScoped<ListarRazas>();
        servicios.AddScoped<ListarLotes>();
        servicios.AddScoped<CrearLote>();
        servicios.AddScoped<ModificarLote>();
        servicios.AddScoped<ListarAnimales>();
        servicios.AddScoped<RegistrarAnimal>();
        servicios.AddScoped<ObtenerFichaAnimal>();
        servicios.AddScoped<ModificarAnimal>();
        servicios.AddScoped<ObtenerHistorialLotes>();
    }

    public void MapearEndpoints(IEndpointRouteBuilder rutas)
    {
        var razas = rutas.MapGroup("/razas").WithTags("trazabilidad");
        razas.MapGet("", (ListarRazas casoDeUso, CancellationToken cancelacion) => casoDeUso.EjecutarAsync(cancelacion));

        var lotes = rutas.MapGroup("/lotes").WithTags("trazabilidad");
        lotes.MapGet("", (ListarLotes casoDeUso, CancellationToken cancelacion) => casoDeUso.EjecutarAsync(cancelacion));
        lotes.MapPost("", async (NuevoLote datos, CrearLote casoDeUso, CancellationToken cancelacion) =>
        {
            var lote = await casoDeUso.EjecutarAsync(datos, cancelacion);
            return TypedResults.Created($"/lotes/{lote.Id}", lote);
        });
        lotes.MapPut("/{id:guid}", (Guid id, CambiosLote cambios, ModificarLote casoDeUso, CancellationToken cancelacion) =>
            casoDeUso.EjecutarAsync(id, cambios, cancelacion));

        var animales = rutas.MapGroup("/animales").WithTags("trazabilidad");
        animales.MapGet("", (
            [FromQuery(Name = "id_lote")] Guid? idLote,
            [FromQuery(Name = "q")] string? busqueda,
            [FromQuery(Name = "con_alertas_activas")] bool? conAlertasActivas,
            ListarAnimales casoDeUso,
            CancellationToken cancelacion) =>
            casoDeUso.EjecutarAsync(new FiltroAnimales(idLote, busqueda, conAlertasActivas ?? false), cancelacion));
        animales.MapPost("", async (NuevoAnimal datos, RegistrarAnimal casoDeUso, CancellationToken cancelacion) =>
        {
            var animal = await casoDeUso.EjecutarAsync(datos, cancelacion);
            return TypedResults.Created($"/animales/{animal.Id}", animal);
        });
        animales.MapGet("/{id:guid}", (Guid id, ObtenerFichaAnimal casoDeUso, CancellationToken cancelacion) =>
            casoDeUso.EjecutarAsync(id, cancelacion));
        animales.MapPut("/{id:guid}", (Guid id, CambiosAnimal cambios, ModificarAnimal casoDeUso, CancellationToken cancelacion) =>
            casoDeUso.EjecutarAsync(id, cambios, cancelacion));
        animales.MapGet("/{id:guid}/historial-lotes", (Guid id, ObtenerHistorialLotes casoDeUso, CancellationToken cancelacion) =>
            casoDeUso.EjecutarAsync(id, cancelacion));
    }
}
