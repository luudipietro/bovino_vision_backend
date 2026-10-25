using BovinoVision.Dominio.Usuarios;
using BovinoVision.Aplicacion.Analitica;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Infraestructura.Analitica;
using Microsoft.AspNetCore.Mvc;

namespace BovinoVision.Api.Modulos.Analitica;

/// <summary>Analítica y Alertas: weighing history with GMD, herd panel, reports and prioritized alerts.</summary>
public sealed class ModuloAnalitica : IModulo
{
    public const string ZonaHorariaPorDefecto = "America/Argentina/Buenos_Aires";

    public void AgregarServicios(IServiceCollection servicios, IConfiguration configuracion)
    {
        var zonaHoraria = TimeZoneInfo.FindSystemTimeZoneById(configuracion["Analitica:ZonaHoraria"] ?? ZonaHorariaPorDefecto);
        var parametros = configuracion.GetSection("Alertas").Get<ParametrosReglasAlerta>() ?? new ParametrosReglasAlerta();

        servicios.AddSingleton(new CalculadoraGmd(zonaHoraria));
        servicios.AddSingleton(new ReglasAlerta(parametros));
        servicios.AddScoped<IEvaluadorAlertas, EvaluadorAlertas>();
        servicios.AddScoped<ConsultarHistorialPesajes>();
        servicios.AddScoped<ListarAlertasDeAnimal>();
        servicios.AddScoped<ListarAlertasActivas>();
        servicios.AddScoped<ResolverAlerta>();
        servicios.AddScoped<ConsultarPanel>();
        servicios.AddScoped<GenerarReporte>();
        servicios.AgregarPersistenciaAnalitica();
    }

    public void MapearEndpoints(IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/animales/{id:guid}/pesajes", async (
            Guid id, ConsultarHistorialPesajes casoDeUso, CancellationToken cancelacion) =>
            Results.Ok(await casoDeUso.EjecutarAsync(new ConsultaHistorialPesajes(id), cancelacion)))
            .WithTags("Analítica")
            .RequierePermiso(Permiso.RodeoConsultar);

        rutas.MapGet("/animales/{id:guid}/alertas", async (
            Guid id, ListarAlertasDeAnimal casoDeUso, CancellationToken cancelacion) =>
            Results.Ok(await casoDeUso.EjecutarAsync(new ConsultaAlertasDeAnimal(id), cancelacion)))
            .WithTags("Analítica")
            .RequierePermiso(Permiso.RodeoConsultar);

        rutas.MapGet("/alertas", async (ListarAlertasActivas casoDeUso, CancellationToken cancelacion) =>
            Results.Ok(await casoDeUso.EjecutarAsync(cancelacion)))
            .WithTags("Analítica")
            .RequierePermiso(Permiso.AnaliticaConsultar);

        rutas.MapPost("/alertas/{id:guid}/resolver", async (
            Guid id, ResolverAlerta casoDeUso, CancellationToken cancelacion) =>
        {
            await casoDeUso.EjecutarAsync(new ResolverAlertaComando(id), cancelacion);
            return Results.NoContent();
        })
            .WithTags("Analítica")
            .RequierePermiso(Permiso.RodeoGestionar);

        rutas.MapGet("/panel", async (
            [FromQuery(Name = "id_lote")] Guid? idLote, ConsultarPanel casoDeUso, CancellationToken cancelacion) =>
            Results.Ok(await casoDeUso.EjecutarAsync(new ConsultaPanel(idLote), cancelacion)))
            .WithTags("Analítica")
            .RequierePermiso(Permiso.AnaliticaConsultar);

        rutas.MapGet("/reportes", async (
            DateOnly desde, DateOnly hasta, [FromQuery(Name = "id_lote")] Guid? idLote,
            GenerarReporte casoDeUso, CancellationToken cancelacion) =>
            Results.Ok(await casoDeUso.EjecutarAsync(new ConsultaReporte(desde, hasta, idLote), cancelacion)))
            .WithTags("Analítica")
            .RequierePermiso(Permiso.AnaliticaConsultar);
    }
}
