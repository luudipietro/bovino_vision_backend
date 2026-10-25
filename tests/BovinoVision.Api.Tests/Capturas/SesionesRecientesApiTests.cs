using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Trazabilidad;
using BovinoVision.Dominio.Usuarios;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Api.Tests.Capturas;

/// <summary>GET /sesiones-captura: the latest capture sessions of the logged-in user.</summary>
public class SesionesRecientesApiTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    private static readonly DateTimeOffset Inicio = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private async Task<Guid> CrearSesionAsync(
        Guid idUsuario, DateTimeOffset inicio, int procesadas = 0, int pendientes = 0, DateTimeOffset? fin = null)
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
        var sesion = new SesionCaptura(idUsuario, inicio);
        if (fin is { } fecha)
        {
            sesion.Finalizar(fecha);
        }

        contexto.SesionesCaptura.Add(sesion);
        for (var i = 0; i < procesadas + pendientes; i++)
        {
            var animal = new Animal(DatosSemilla.IdEstablecimiento, $"AR-{sesion.Id:N}-{i}");
            var captura = new Captura(animal.Id, idUsuario, sesion.Id, inicio.AddMinutes(i));
            captura.AgregarImagen($"capturas/{captura.Id}.jpg", Guid.NewGuid().ToString("N"), "image/jpeg");
            if (i < procesadas)
            {
                captura.SolicitarEstimacion();
                captura.IniciarProcesamiento();
                captura.MarcarProcesada();
            }

            contexto.AddRange(animal, captura);
        }

        await contexto.SaveChangesAsync(Cancelacion);
        return sesion.Id;
    }

    private async Task<JsonElement> ListarAsync(string ruta = "/sesiones-captura")
    {
        using var respuesta = await Cliente.GetAsync(new Uri(ruta, UriKind.Relative), Cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.LeerAsync<JsonElement>();
    }

    [Fact]
    public async Task Lista_las_sesiones_del_usuario_de_la_mas_nueva_a_la_mas_vieja_con_sus_cantidades()
    {
        var vieja = await CrearSesionAsync(Fabrica.IdUsuarioPruebas, Inicio, procesadas: 1, pendientes: 1, fin: Inicio.AddHours(1));
        var nueva = await CrearSesionAsync(Fabrica.IdUsuarioPruebas, Inicio.AddDays(1), procesadas: 2);

        var sesiones = await ListarAsync();

        Assert.Equal([nueva, vieja], sesiones.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()));
        var primera = sesiones[0];
        Assert.Equal(Inicio.AddDays(1), primera.GetProperty("fecha_inicio").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, primera.GetProperty("fecha_fin").ValueKind);
        Assert.Equal("activa", primera.GetProperty("estado").GetString());
        Assert.Equal(2, primera.GetProperty("cantidad_capturas").GetInt32());
        Assert.Equal(2, primera.GetProperty("cantidad_procesadas").GetInt32());
        var segunda = sesiones[1];
        Assert.Equal(Inicio.AddHours(1), segunda.GetProperty("fecha_fin").GetDateTimeOffset());
        Assert.Equal("finalizada", segunda.GetProperty("estado").GetString());
        Assert.Equal(2, segunda.GetProperty("cantidad_capturas").GetInt32());
        Assert.Equal(1, segunda.GetProperty("cantidad_procesadas").GetInt32());
    }

    [Fact]
    public async Task Una_sesion_sin_capturas_cuenta_cero()
    {
        await CrearSesionAsync(Fabrica.IdUsuarioPruebas, Inicio);

        var sesion = Assert.Single((await ListarAsync()).EnumerateArray());

        Assert.Equal(0, sesion.GetProperty("cantidad_capturas").GetInt32());
        Assert.Equal(0, sesion.GetProperty("cantidad_procesadas").GetInt32());
    }

    [Fact]
    public async Task No_incluye_las_sesiones_de_otros_usuarios()
    {
        var propia = await CrearSesionAsync(Fabrica.IdUsuarioPruebas, Inicio);
        await CrearSesionAsync(DatosSemilla.IdUsuarioSistema, Inicio.AddDays(1), procesadas: 1);

        var sesiones = await ListarAsync();

        Assert.Equal(propia, Assert.Single(sesiones.EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Por_defecto_devuelve_las_cinco_mas_recientes_y_limite_la_acota()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 7; i++)
        {
            ids.Add(await CrearSesionAsync(Fabrica.IdUsuarioPruebas, Inicio.AddDays(i)));
        }

        var porDefecto = await ListarAsync();
        var dos = await ListarAsync("/sesiones-captura?limite=2");
        var todas = await ListarAsync("/sesiones-captura?limite=20");

        Assert.Equal(5, porDefecto.GetArrayLength());
        Assert.Equal(ids[6], porDefecto[0].GetProperty("id").GetGuid());
        Assert.Equal(ids[2], porDefecto[4].GetProperty("id").GetGuid());
        Assert.Equal([ids[6], ids[5]], dos.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()));
        Assert.Equal(7, todas.GetArrayLength());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("21")]
    [InlineData("-1")]
    public async Task Un_limite_fuera_de_1_a_20_responde_422(string limite)
    {
        using var respuesta = await Cliente.GetAsync(new Uri($"/sesiones-captura?limite={limite}", UriKind.Relative), Cancelacion);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        var cuerpo = await respuesta.LeerAsync<JsonElement>();
        Assert.Contains("limite", cuerpo.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_limite_que_no_es_un_numero_responde_400()
    {
        using var respuesta = await Cliente.GetAsync(new Uri("/sesiones-captura?limite=muchas", UriKind.Relative), Cancelacion);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_sesion_responde_401_y_sin_capturas_registrar_403()
    {
        using var anonimo = Fabrica.CrearClienteAnonimo();
        using var sinSesion = await anonimo.GetAsync(new Uri("/sesiones-captura", UriKind.Relative), Cancelacion);

        // The initial administrator role manages users and roles only.
        using var administrador = Fabrica.CrearClienteConSesion(await IniciarSesionComoAdministradorAsync());
        using var sinPermiso = await administrador.GetAsync(new Uri("/sesiones-captura", UriKind.Relative), Cancelacion);

        Assert.Equal(HttpStatusCode.Unauthorized, sinSesion.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, sinPermiso.StatusCode);
    }

    private async Task<string> IniciarSesionComoAdministradorAsync()
    {
        const string Correo = "admin-sesiones@campo.com";
        const string Contrasena = "admin-sesiones-123";
        await using (var alcance = Fabrica.Services.CreateAsyncScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
            var hasher = alcance.ServiceProvider.GetRequiredService<IHasherContrasenas>();
            contexto.Usuarios.Add(new Usuario(
                DatosSemilla.IdEstablecimiento, DatosSemilla.IdRolAdministrador, "Administrador", Correo, hasher.Generar(Contrasena)));
            await contexto.SaveChangesAsync(Cancelacion);
        }

        using var anonimo = Fabrica.CrearClienteAnonimo();
        using var login = await anonimo.PostAsJsonAsync("/auth/sesion", new { correo = Correo, contrasena = Contrasena }, Cancelacion);
        login.EnsureSuccessStatusCode();
        return FabricaApi.LeerCookieDeSesion(login)!;
    }
}
