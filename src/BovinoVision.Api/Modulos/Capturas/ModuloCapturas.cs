using BovinoVision.Dominio.Usuarios;
using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Dominio.Comun;
using BovinoVision.Infraestructura.Capturas;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BovinoVision.Api.Modulos.Capturas;

/// <summary>Capture sessions, photo uploads and downloads and their gallery (CU-12, CU-13), including discarding a photo that was not sent to estimation.</summary>
internal sealed class ModuloCapturas : IModulo
{
    public void AgregarServicios(IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddScoped<IniciarSesionCaptura>();
        servicios.AddScoped<FinalizarSesionCaptura>();
        servicios.AddScoped<ListarCapturasDeSesion>();
        servicios.AddScoped<ListarSesionesRecientes>();
        servicios.AddScoped<RegistrarCaptura>();
        servicios.AddScoped<ObtenerCaptura>();
        servicios.AddScoped<ConfirmarCaptura>();
        servicios.AddScoped<DescartarCaptura>();
        servicios.AddScoped<ObtenerImagen>();
        servicios.AgregarInfraestructuraCapturas(configuracion);
    }

    public void MapearEndpoints(IEndpointRouteBuilder rutas)
    {
        var sesiones = rutas.MapGroup("/sesiones-captura").WithTags("capturas").RequierePermiso(Permiso.CapturasRegistrar);
        sesiones.MapPost("", IniciarSesionAsync);
        sesiones.MapGet("", ListarSesionesAsync);
        sesiones.MapPost("/{id:guid}/finalizar", FinalizarSesionAsync);
        sesiones.MapGet("/{id:guid}/capturas", ListarCapturasAsync);

        var capturas = rutas.MapGroup("/capturas").WithTags("capturas").RequierePermiso(Permiso.CapturasRegistrar);
        capturas.MapPost("", RegistrarCapturaAsync).DisableAntiforgery();
        capturas.MapGet("/{id:guid}", ObtenerCapturaAsync);
        capturas.MapPost("/{id:guid}/confirmar", ConfirmarCapturaAsync);
        capturas.MapDelete("/{id:guid}", DescartarCapturaAsync);

        // Outside the /capturas group: group and endpoint policies combine with AND, and viewing a photo
        // also belongs to sessions that only consult the herd.
        rutas.MapGet("/imagenes/{id:guid}", ObtenerImagenAsync)
            .WithTags("capturas")
            .RequiereAlgunPermiso(Permiso.CapturasRegistrar, Permiso.RodeoConsultar);
    }

    private static async Task<FileStreamHttpResult> ObtenerImagenAsync(
        Guid id,
        ObtenerImagen casoDeUso,
        HttpResponse respuesta,
        CancellationToken cancelacion)
    {
        var archivo = await casoDeUso.EjecutarAsync(new ObtenerImagenConsulta(id), cancelacion);
        respuesta.Headers.CacheControl = "private";
        return TypedResults.File(archivo.Contenido, archivo.TipoContenido);
    }

    private static async Task<Created<SesionCapturaRespuesta>> IniciarSesionAsync(
        IniciarSesionCaptura casoDeUso,
        CancellationToken cancelacion)
    {
        var sesion = await casoDeUso.EjecutarAsync(cancelacion);
        return TypedResults.Created($"/sesiones-captura/{sesion.Id}", sesion);
    }

    private static async Task<Ok<IReadOnlyList<SesionRecienteRespuesta>>> ListarSesionesAsync(
        int? limite,
        ListarSesionesRecientes casoDeUso,
        CancellationToken cancelacion) =>
        TypedResults.Ok(await casoDeUso.EjecutarAsync(new ListarSesionesRecientesConsulta(limite), cancelacion));

    private static async Task<Ok<SesionCapturaRespuesta>> FinalizarSesionAsync(
        Guid id,
        FinalizarSesionCaptura casoDeUso,
        CancellationToken cancelacion) =>
        TypedResults.Ok(await casoDeUso.EjecutarAsync(new FinalizarSesionCapturaComando(id), cancelacion));

    private static async Task<Ok<IReadOnlyList<CapturaDeSesionRespuesta>>> ListarCapturasAsync(
        Guid id,
        ListarCapturasDeSesion casoDeUso,
        CancellationToken cancelacion) =>
        TypedResults.Ok(await casoDeUso.EjecutarAsync(new ListarCapturasDeSesionConsulta(id), cancelacion));

    private static async Task<Created<CapturaRespuesta>> RegistrarCapturaAsync(
        [FromForm(Name = "id_sesion")] Guid idSesion,
        [FromForm(Name = "id_animal")] Guid idAnimal,
        [FromForm(Name = "fecha_captura")] DateTimeOffset? fechaCaptura,
        IFormFile? imagen,
        RegistrarCaptura casoDeUso,
        CancellationToken cancelacion)
    {
        if (imagen is null)
        {
            throw new OperacionInvalidaError("La imagen es obligatoria");
        }

        using var contenido = new MemoryStream();
        await imagen.CopyToAsync(contenido, cancelacion);

        var captura = await casoDeUso.EjecutarAsync(
            new RegistrarCapturaComando(idSesion, idAnimal, imagen.FileName, imagen.ContentType ?? "", contenido.ToArray(), fechaCaptura),
            cancelacion);
        return TypedResults.Created($"/capturas/{captura.Id}", captura);
    }

    private static async Task<Ok<CapturaRespuesta>> ConfirmarCapturaAsync(
        Guid id,
        ConfirmarCaptura confirmar,
        ObtenerCaptura obtener,
        CancellationToken cancelacion)
    {
        await confirmar.EjecutarAsync(new ConfirmarCapturaComando(id), cancelacion);
        return TypedResults.Ok(await obtener.EjecutarAsync(new ObtenerCapturaConsulta(id), cancelacion));
    }

    private static async Task<NoContent> DescartarCapturaAsync(
        Guid id,
        DescartarCaptura casoDeUso,
        CancellationToken cancelacion)
    {
        await casoDeUso.EjecutarAsync(new DescartarCapturaComando(id), cancelacion);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<CapturaRespuesta>> ObtenerCapturaAsync(
        Guid id,
        ObtenerCaptura casoDeUso,
        CancellationToken cancelacion) =>
        TypedResults.Ok(await casoDeUso.EjecutarAsync(new ObtenerCapturaConsulta(id), cancelacion));
}
