using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Api.Tests.Usuarios;

/// <summary>El DTO de usuario lleva ultimo_acceso: la hora de creación de la sesión de login más reciente del usuario.</summary>
public class UltimoAccesoApiTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    private const string Correo = "ana@campo.com";
    private const string Contrasena = "segura-123";

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private async Task<Guid> RegistrarAsync()
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            "/usuarios", new { nombre = "Ana Pérez", correo = Correo, contrasena = Contrasena, id_rol = DatosSemilla.IdRolOperario }, Cancelacion);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = await respuesta.LeerAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, creado.GetProperty("ultimo_acceso").ValueKind);
        return creado.GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> IniciarSesionAsync()
    {
        using var anonimo = Fabrica.CrearClienteAnonimo();
        using var respuesta = await anonimo.PostAsJsonAsync("/auth/sesion", new { correo = Correo, contrasena = Contrasena }, Cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.LeerAsync<JsonElement>()).GetProperty("usuario");
    }

    private async Task<JsonElement> ListarUsuarioAsync(Guid id)
    {
        var usuarios = await Cliente.GetFromJsonAsync<JsonElement>(new Uri("/usuarios", UriKind.Relative), Cancelacion);
        return usuarios.EnumerateArray().Single(u => u.GetProperty("id").GetGuid() == id);
    }

    private async Task<DateTimeOffset> UltimaSesionEnBaseAsync(Guid idUsuario)
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
        return await contexto.SesionesUsuario.Where(s => s.IdUsuario == idUsuario).MaxAsync(s => s.CreadaEn, Cancelacion);
    }

    [Fact]
    public async Task El_listado_trae_nulo_para_quien_nunca_inicio_sesion_y_la_sesion_mas_reciente_para_el_resto()
    {
        var idAna = await RegistrarAsync();
        var sinIngresar = await ListarUsuarioAsync(idAna);
        await IniciarSesionAsync();
        var primera = await UltimaSesionEnBaseAsync(idAna);
        await Task.Delay(50, Cancelacion);
        await IniciarSesionAsync();
        var segunda = await UltimaSesionEnBaseAsync(idAna);

        var conIngresos = await ListarUsuarioAsync(idAna);
        var delUsuarioDePruebas = await ListarUsuarioAsync(Fabrica.IdUsuarioPruebas);

        Assert.Equal(JsonValueKind.Null, sinIngresar.GetProperty("ultimo_acceso").ValueKind);
        Assert.True(segunda > primera);
        Assert.Equal(segunda, conIngresos.GetProperty("ultimo_acceso").GetDateTimeOffset());
        Assert.NotEqual(JsonValueKind.Null, delUsuarioDePruebas.GetProperty("ultimo_acceso").ValueKind);
    }

    [Fact]
    public async Task El_inicio_de_sesion_y_la_sesion_actual_traen_el_ultimo_acceso()
    {
        var idAna = await RegistrarAsync();
        var delLogin = await IniciarSesionAsync();
        using var anonimo = Fabrica.CrearClienteAnonimo();
        using var login = await anonimo.PostAsJsonAsync("/auth/sesion", new { correo = Correo, contrasena = Contrasena }, Cancelacion);
        using var conSesion = Fabrica.CrearClienteConSesion(FabricaApi.LeerCookieDeSesion(login)!);

        var actual = (await conSesion.GetFromJsonAsync<JsonElement>(new Uri("/auth/sesion", UriKind.Relative), Cancelacion)).GetProperty("usuario");

        var esperado = await UltimaSesionEnBaseAsync(idAna);
        Assert.NotEqual(JsonValueKind.Null, delLogin.GetProperty("ultimo_acceso").ValueKind);
        Assert.Equal(esperado, actual.GetProperty("ultimo_acceso").GetDateTimeOffset());
    }

    [Fact]
    public async Task Modificar_cambiar_el_rol_y_dar_de_baja_devuelven_el_mismo_ultimo_acceso()
    {
        var idAna = await RegistrarAsync();
        await IniciarSesionAsync();
        var esperado = await UltimaSesionEnBaseAsync(idAna);

        using var modificado = await Cliente.PutAsJsonAsync($"/usuarios/{idAna}", new { nombre = "Ana Gómez" }, Cancelacion);
        using var conRol = await Cliente.PutAsJsonAsync($"/usuarios/{idAna}/rol", new { id_rol = DatosSemilla.IdRolProductor }, Cancelacion);
        using var baja = await Cliente.PostAsync(new Uri($"/usuarios/{idAna}/baja", UriKind.Relative), null, Cancelacion);

        foreach (var respuesta in new[] { modificado, conRol, baja })
        {
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.Equal(esperado, (await respuesta.LeerAsync<JsonElement>()).GetProperty("ultimo_acceso").GetDateTimeOffset());
        }
    }
}
