using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Dominio.Usuarios;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Api.Tests.Usuarios;

public class AutenticacionApiTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    private const string Contrasena = "operario-123";

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private async Task<Usuario> CrearUsuarioAsync(Guid idRol, string correo = "operario@campo.com")
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
        var hasher = alcance.ServiceProvider.GetRequiredService<IHasherContrasenas>();
        var usuario = new Usuario(DatosSemilla.IdEstablecimiento, idRol, "Operario Uno", correo, hasher.Generar(Contrasena));
        contexto.Usuarios.Add(usuario);
        await contexto.SaveChangesAsync(Cancelacion);
        return usuario;
    }

    private async Task ModificarAsync(Func<BovinoVisionDbContext, Task> cambio)
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        await cambio(alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>());
    }

    private static async Task<HttpResponseMessage> IngresarAsync(HttpClient cliente, string correo, string contrasena) =>
        await cliente.PostAsJsonAsync("/auth/sesion", new { correo, contrasena }, Cancelacion);

    private async Task<string> IniciarSesionAsync(string correo = "operario@campo.com")
    {
        using var anonimo = Fabrica.CrearClienteAnonimo();
        using var respuesta = await IngresarAsync(anonimo, correo, Contrasena);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return FabricaApi.LeerCookieDeSesion(respuesta)!;
    }

    private static async Task<string?> DetalleAsync(HttpResponseMessage respuesta) =>
        (await respuesta.LeerAsync<JsonElement>()).GetProperty("detail").GetString();

    [Fact]
    public async Task Iniciar_sesion_entrega_una_cookie_protegida_y_los_permisos_del_rol()
    {
        var usuario = await CrearUsuarioAsync(DatosSemilla.IdRolOperario);
        using var anonimo = Fabrica.CrearClienteAnonimo();

        using var respuesta = await IngresarAsync(anonimo, " OPERARIO@campo.com", Contrasena);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cookie = Assert.Single(respuesta.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("bovinovision_sesion=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        var texto = await respuesta.Content.ReadAsStringAsync(Cancelacion);
        Assert.DoesNotContain(FabricaApi.LeerCookieDeSesion(respuesta)!, texto, StringComparison.Ordinal);
        var cuerpo = JsonDocument.Parse(texto).RootElement;
        Assert.Equal(usuario.Id, cuerpo.GetProperty("usuario").GetProperty("id").GetGuid());
        Assert.Equal("Operario de Campo", cuerpo.GetProperty("usuario").GetProperty("rol").GetString());
        Assert.Equal(
            ["capturas_registrar", "rodeo_gestionar", "rodeo_consultar"],
            cuerpo.GetProperty("permisos").EnumerateArray().Select(p => p.GetString()));
        Assert.True(cuerpo.TryGetProperty("expira_en", out _));
    }

    [Fact]
    public async Task La_sesion_actual_se_consulta_con_la_cookie_y_sin_ella_responde_401()
    {
        await CrearUsuarioAsync(DatosSemilla.IdRolOperario);
        using var conSesion = Fabrica.CrearClienteConSesion(await IniciarSesionAsync());
        using var anonimo = Fabrica.CrearClienteAnonimo();

        using var actual = await conSesion.GetAsync(new Uri("/auth/sesion", UriKind.Relative), Cancelacion);
        using var sinCookie = await anonimo.GetAsync(new Uri("/auth/sesion", UriKind.Relative), Cancelacion);

        Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
        Assert.Equal("operario@campo.com", (await actual.LeerAsync<JsonElement>()).GetProperty("usuario").GetProperty("correo").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, sinCookie.StatusCode);
        Assert.Equal("Iniciá sesión para continuar.", await DetalleAsync(sinCookie));
    }

    [Fact]
    public async Task Correo_desconocido_y_contrasena_incorrecta_responden_401_con_el_mismo_mensaje()
    {
        await CrearUsuarioAsync(DatosSemilla.IdRolOperario);
        using var anonimo = Fabrica.CrearClienteAnonimo();

        using var desconocido = await IngresarAsync(anonimo, "nadie@campo.com", Contrasena);
        using var incorrecta = await IngresarAsync(anonimo, "operario@campo.com", "otra-clave");

        Assert.Equal(HttpStatusCode.Unauthorized, desconocido.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, incorrecta.StatusCode);
        Assert.Equal("Usuario o contraseña incorrectos.", await DetalleAsync(desconocido));
        Assert.Equal("Usuario o contraseña incorrectos.", await DetalleAsync(incorrecta));
        Assert.False(incorrecta.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task El_quinto_intento_fallido_bloquea_cinco_minutos_aun_con_la_contrasena_correcta()
    {
        await CrearUsuarioAsync(DatosSemilla.IdRolOperario);
        using var anonimo = Fabrica.CrearClienteAnonimo();
        for (var intento = 1; intento < Usuario.MaximoIntentosFallidos; intento++)
        {
            using var fallido = await IngresarAsync(anonimo, "operario@campo.com", "otra-clave");
            Assert.Equal(HttpStatusCode.Unauthorized, fallido.StatusCode);
        }

        using var quinto = await IngresarAsync(anonimo, "operario@campo.com", "otra-clave");
        using var correcta = await IngresarAsync(anonimo, "operario@campo.com", Contrasena);

        Assert.Equal(HttpStatusCode.TooManyRequests, quinto.StatusCode);
        Assert.Equal("El usuario está bloqueado por intentos fallidos. Volvé a intentar en 5 minutos.", await DetalleAsync(quinto));
        Assert.Equal(TimeSpan.FromMinutes(5), quinto.Headers.RetryAfter?.Delta);
        Assert.Equal(HttpStatusCode.TooManyRequests, correcta.StatusCode);
        Assert.InRange(correcta.Headers.RetryAfter!.Delta!.Value, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Una_cuenta_dada_de_baja_responde_403_y_corta_sus_sesiones_abiertas()
    {
        var usuario = await CrearUsuarioAsync(DatosSemilla.IdRolOperario);
        using var conSesion = Fabrica.CrearClienteConSesion(await IniciarSesionAsync());
        await ModificarAsync(async contexto =>
        {
            var guardado = await contexto.Usuarios.SingleAsync(u => u.Id == usuario.Id, Cancelacion);
            guardado.DarDeBaja();
            await contexto.SaveChangesAsync(Cancelacion);
        });
        using var anonimo = Fabrica.CrearClienteAnonimo();

        using var ingreso = await IngresarAsync(anonimo, "operario@campo.com", Contrasena);
        using var abierta = await conSesion.GetAsync(new Uri("/animales", UriKind.Relative), Cancelacion);

        Assert.Equal(HttpStatusCode.Forbidden, ingreso.StatusCode);
        Assert.Equal("No es posible acceder con esta cuenta. Contactá a un administrador.", await DetalleAsync(ingreso));
        Assert.Equal(HttpStatusCode.Unauthorized, abierta.StatusCode);
    }

    [Fact]
    public async Task Cerrar_sesion_invalida_el_token_y_borra_la_cookie()
    {
        await CrearUsuarioAsync(DatosSemilla.IdRolOperario);
        using var conSesion = Fabrica.CrearClienteConSesion(await IniciarSesionAsync());

        using var cierre = await conSesion.DeleteAsync(new Uri("/auth/sesion", UriKind.Relative), Cancelacion);
        using var despues = await conSesion.GetAsync(new Uri("/auth/sesion", UriKind.Relative), Cancelacion);

        Assert.Equal(HttpStatusCode.NoContent, cierre.StatusCode);
        var cookie = Assert.Single(cierre.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("bovinovision_sesion=;", cookie, StringComparison.Ordinal);
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
    }

    [Fact]
    public async Task Los_endpoints_exigen_sesion_y_permiso_salvo_salud_e_inicio_de_sesion()
    {
        await CrearUsuarioAsync(DatosSemilla.IdRolOperario);
        using var operario = Fabrica.CrearClienteConSesion(await IniciarSesionAsync());
        using var anonimo = Fabrica.CrearClienteAnonimo();

        using var salud = await anonimo.GetAsync(new Uri("/health", UriKind.Relative), Cancelacion);
        using var animalesAnonimo = await anonimo.GetAsync(new Uri("/animales", UriKind.Relative), Cancelacion);
        using var animales = await operario.GetAsync(new Uri("/animales", UriKind.Relative), Cancelacion);
        using var razas = await operario.GetAsync(new Uri("/razas", UriKind.Relative), Cancelacion);
        using var razasAnonimo = await anonimo.GetAsync(new Uri("/razas", UriKind.Relative), Cancelacion);
        using var panel = await operario.GetAsync(new Uri("/panel", UriKind.Relative), Cancelacion);
        using var lote = await operario.PostAsJsonAsync("/lotes", new { codigo = "OP-1" }, Cancelacion);

        Assert.Equal(HttpStatusCode.OK, salud.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, animalesAnonimo.StatusCode);
        Assert.Equal(HttpStatusCode.OK, animales.StatusCode);
        Assert.Equal(HttpStatusCode.OK, razas.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, razasAnonimo.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, panel.StatusCode);
        Assert.Equal("No tenés permiso para esta operación.", await DetalleAsync(panel));
        Assert.Equal(HttpStatusCode.Created, lote.StatusCode);
    }

    [Fact]
    public async Task Un_cambio_de_permisos_del_rol_se_aplica_en_el_request_siguiente()
    {
        await CrearUsuarioAsync(DatosSemilla.IdRolOperario);
        using var operario = Fabrica.CrearClienteConSesion(await IniciarSesionAsync());
        using var antes = await operario.GetAsync(new Uri("/panel", UriKind.Relative), Cancelacion);

        await ModificarAsync(async contexto =>
        {
            var rol = await contexto.Roles.SingleAsync(r => r.Id == DatosSemilla.IdRolOperario, Cancelacion);
            rol.DefinirPermisos([.. rol.Permisos, Permiso.AnaliticaConsultar]);
            await contexto.SaveChangesAsync(Cancelacion);
        });
        using var despues = await operario.GetAsync(new Uri("/panel", UriKind.Relative), Cancelacion);

        Assert.Equal(HttpStatusCode.Forbidden, antes.StatusCode);
        Assert.Equal(HttpStatusCode.OK, despues.StatusCode);
    }

    [Fact]
    public async Task El_administrador_inicial_se_crea_desde_la_configuracion_al_arrancar()
    {
        await using var host = Fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Autenticacion:AdministradorInicial:Correo", "Admin@Campo.com");
            builder.UseSetting("Autenticacion:AdministradorInicial:Nombre", "Administradora");
            builder.UseSetting("Autenticacion:AdministradorInicial:Contrasena", "clave-inicial");
        });
        _ = host.Services;
        using var anonimo = Fabrica.CrearClienteAnonimo();

        using var respuesta = await IngresarAsync(anonimo, "admin@campo.com", "clave-inicial");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.LeerAsync<JsonElement>();
        Assert.Equal("Administrador", cuerpo.GetProperty("usuario").GetProperty("rol").GetString());
        Assert.Equal(
            ["usuarios_gestionar", "roles_gestionar", "auditoria_consultar"],
            cuerpo.GetProperty("permisos").EnumerateArray().Select(p => p.GetString()));
    }
}
