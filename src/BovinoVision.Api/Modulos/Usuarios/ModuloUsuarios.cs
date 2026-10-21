using Microsoft.AspNetCore.Mvc;
using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Dominio.Usuarios;
using BovinoVision.Infraestructura.Seguridad;
using BovinoVision.Infraestructura.Usuarios;

namespace BovinoVision.Api.Modulos.Usuarios;

/// <summary>Usuarios module: login sessions (CU-01, CU-02), users, roles and permissions (CU-03 to CU-08).</summary>
public sealed class ModuloUsuarios : IModulo
{
    public void AgregarServicios(IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddScoped<IniciarSesion>();
        servicios.AddScoped<CerrarSesion>();
        servicios.AddScoped<ValidarSesion>();
        servicios.AddScoped<CrearAdministradorInicial>();
        servicios.AddScoped<ListarUsuarios>();
        servicios.AddScoped<RegistrarUsuario>();
        servicios.AddScoped<ModificarUsuario>();
        servicios.AddScoped<AsignarRolUsuario>();
        servicios.AddScoped<DarDeBajaUsuario>();
        servicios.AddScoped<ListarRoles>();
        servicios.AddSingleton<ListarPermisos>();
        servicios.AddScoped<DefinirPermisosRol>();
        servicios.AgregarInfraestructuraUsuarios(configuracion);
    }

    public void MapearEndpoints(IEndpointRouteBuilder rutas)
    {
        var sesion = rutas.MapGroup("/auth/sesion").WithTags("autenticacion");
        sesion.MapPost("", IniciarAsync).AllowAnonymous();
        sesion.MapGet("", Obtener);
        sesion.MapDelete("", CerrarAsync);

        var usuarios = rutas.MapGroup("/usuarios").WithTags("usuarios").RequierePermiso(Permiso.UsuariosGestionar);
        usuarios.MapGet("", ([FromQuery(Name = "id_rol")] Guid? idRol, ListarUsuarios casoDeUso, CancellationToken cancelacion) =>
            casoDeUso.EjecutarAsync(idRol, cancelacion));
        usuarios.MapPost("", async (NuevoUsuario datos, RegistrarUsuario casoDeUso, CancellationToken cancelacion) =>
        {
            var usuario = await casoDeUso.EjecutarAsync(datos, cancelacion);
            return TypedResults.Created($"/usuarios/{usuario.Id}", usuario);
        });
        usuarios.MapPut("/{id:guid}", (Guid id, CambiosUsuario cambios, ModificarUsuario casoDeUso, CancellationToken cancelacion) =>
            casoDeUso.EjecutarAsync(id, cambios, cancelacion));
        usuarios.MapPut("/{id:guid}/rol", (Guid id, CambioRol cambio, AsignarRolUsuario casoDeUso, CancellationToken cancelacion) =>
            casoDeUso.EjecutarAsync(id, cambio, cancelacion));
        usuarios.MapPost("/{id:guid}/baja", (Guid id, DarDeBajaUsuario casoDeUso, CancellationToken cancelacion) =>
            casoDeUso.EjecutarAsync(id, cancelacion));

        var roles = rutas.MapGroup("/roles").WithTags("usuarios");
        roles.MapGet("", (ListarRoles casoDeUso, CancellationToken cancelacion) => casoDeUso.EjecutarAsync(cancelacion))
            .RequiereAlgunPermiso(Permiso.RolesGestionar, Permiso.UsuariosGestionar);
        roles.MapPut("/{id:guid}/permisos", (Guid id, PermisosRol permisos, DefinirPermisosRol casoDeUso, CancellationToken cancelacion) =>
            casoDeUso.EjecutarAsync(id, permisos, cancelacion))
            .RequierePermiso(Permiso.RolesGestionar);
        rutas.MapGet("/permisos", (ListarPermisos casoDeUso) => casoDeUso.Ejecutar())
            .WithTags("usuarios")
            .RequiereAlgunPermiso(Permiso.RolesGestionar, Permiso.UsuariosGestionar);
    }

    private static async Task<IResult> IniciarAsync(
        CredencialesUsuario credenciales, IniciarSesion casoDeUso, HttpContext contexto, CancellationToken cancelacion)
    {
        var iniciada = await casoDeUso.EjecutarAsync(credenciales, cancelacion);
        contexto.Response.Cookies.Append(SesionHttp.Cookie, iniciada.Token, SesionHttp.OpcionesCookie(iniciada.Sesion.ExpiraEn));
        return Results.Ok(iniciada.Sesion);
    }

    private static IResult Obtener(HttpContext contexto) =>
        contexto.SesionActual() is { } sesion ? Results.Ok(sesion.ARespuesta()) : Results.Unauthorized();

    private static async Task<IResult> CerrarAsync(HttpContext contexto, CerrarSesion casoDeUso, CancellationToken cancelacion)
    {
        if (contexto.Request.Cookies.TryGetValue(SesionHttp.Cookie, out var token) && !string.IsNullOrEmpty(token))
        {
            await casoDeUso.EjecutarAsync(token, cancelacion);
        }

        contexto.Response.Cookies.Delete(SesionHttp.Cookie, SesionHttp.OpcionesCookie(DateTimeOffset.UnixEpoch));
        return Results.NoContent();
    }
}
