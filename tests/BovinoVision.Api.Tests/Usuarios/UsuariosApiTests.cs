using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Infraestructura.Persistencia;

namespace BovinoVision.Api.Tests.Usuarios;

public class UsuariosApiTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private static object Permisos(params string[] permisos) => new { permisos };

    private async Task<JsonElement> RegistrarAsync(string correo, Guid idRol, string contrasena = "segura-123")
    {
        using var respuesta = await Cliente.PostAsJsonAsync(
            "/usuarios", new { nombre = "Ana Pérez", correo, contrasena, id_rol = idRol }, Cancelacion);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await respuesta.LeerAsync<JsonElement>();
    }

    private static async Task<string?> DetalleAsync(HttpResponseMessage respuesta) =>
        (await respuesta.LeerAsync<JsonElement>()).GetProperty("detail").GetString();

    private async Task<HttpStatusCode> IngresarAsync(string correo, string contrasena)
    {
        using var anonimo = Fabrica.CrearClienteAnonimo();
        using var respuesta = await anonimo.PostAsJsonAsync("/auth/sesion", new { correo, contrasena }, Cancelacion);
        return respuesta.StatusCode;
    }

    [Fact]
    public async Task Registrar_un_usuario_lo_crea_activo_y_puede_iniciar_sesion()
    {
        var creado = await RegistrarAsync("Ana@Campo.com", DatosSemilla.IdRolOperario);

        Assert.Equal("ana@campo.com", creado.GetProperty("correo").GetString());
        Assert.Equal(DatosSemilla.IdRolOperario, creado.GetProperty("id_rol").GetGuid());
        Assert.Equal("Operario de Campo", creado.GetProperty("rol").GetString());
        Assert.True(creado.GetProperty("activo").GetBoolean());
        Assert.Equal(JsonValueKind.Null, creado.GetProperty("bloqueado_hasta").ValueKind);
        Assert.False(creado.TryGetProperty("resumen_contrasena", out _));
        Assert.Equal(HttpStatusCode.OK, await IngresarAsync("ana@campo.com", "segura-123"));
    }

    [Fact]
    public async Task Registrar_rechaza_un_correo_repetido_y_una_contrasena_corta()
    {
        await RegistrarAsync("ana@campo.com", DatosSemilla.IdRolOperario);

        using var repetido = await Cliente.PostAsJsonAsync(
            "/usuarios", new { nombre = "Otra", correo = "ANA@campo.com", contrasena = "segura-123", id_rol = DatosSemilla.IdRolOperario }, Cancelacion);
        using var corta = await Cliente.PostAsJsonAsync(
            "/usuarios", new { nombre = "Otra", correo = "otra@campo.com", contrasena = "corta", id_rol = DatosSemilla.IdRolOperario }, Cancelacion);

        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Equal("Ya existe un usuario con el correo ana@campo.com", await DetalleAsync(repetido));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, corta.StatusCode);
        Assert.Equal("La contraseña debe tener al menos 8 caracteres", await DetalleAsync(corta));
    }

    [Fact]
    public async Task El_listado_filtra_por_rol()
    {
        await RegistrarAsync("ana@campo.com", DatosSemilla.IdRolOperario);
        await RegistrarAsync("beto@campo.com", DatosSemilla.IdRolProductor);

        var operarios = await Cliente.GetFromJsonAsync<JsonElement>($"/usuarios?id_rol={DatosSemilla.IdRolOperario}", Cancelacion);
        var todos = await Cliente.GetFromJsonAsync<JsonElement>("/usuarios", Cancelacion);

        Assert.Equal(["ana@campo.com"], operarios.EnumerateArray().Select(u => u.GetProperty("correo").GetString()));
        Assert.Contains("beto@campo.com", todos.EnumerateArray().Select(u => u.GetProperty("correo").GetString()));
        Assert.Contains("sistema@bovinovision.local", todos.EnumerateArray().Select(u => u.GetProperty("correo").GetString()));
    }

    [Fact]
    public async Task Modificar_y_cambiar_el_rol_aplican_sobre_el_usuario()
    {
        var id = (await RegistrarAsync("ana@campo.com", DatosSemilla.IdRolOperario)).GetProperty("id").GetGuid();

        using var modificado = await Cliente.PutAsJsonAsync($"/usuarios/{id}", new { nombre = "Ana María", contrasena = "nueva-clave" }, Cancelacion);
        using var rol = await Cliente.PutAsJsonAsync($"/usuarios/{id}/rol", new { id_rol = DatosSemilla.IdRolProductor }, Cancelacion);
        using var mismoRol = await Cliente.PutAsJsonAsync($"/usuarios/{id}/rol", new { id_rol = DatosSemilla.IdRolProductor }, Cancelacion);

        Assert.Equal(HttpStatusCode.OK, modificado.StatusCode);
        Assert.Equal("Ana María", (await modificado.LeerAsync<JsonElement>()).GetProperty("nombre").GetString());
        Assert.Equal("Productor Ganadero", (await rol.LeerAsync<JsonElement>()).GetProperty("rol").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, mismoRol.StatusCode);
        Assert.Equal("El usuario ya tiene ese rol", await DetalleAsync(mismoRol));
        Assert.Equal(HttpStatusCode.Unauthorized, await IngresarAsync("ana@campo.com", "segura-123"));
        Assert.Equal(HttpStatusCode.OK, await IngresarAsync("ana@campo.com", "nueva-clave"));
    }

    [Fact]
    public async Task La_baja_es_logica_e_impide_iniciar_sesion()
    {
        var id = (await RegistrarAsync("ana@campo.com", DatosSemilla.IdRolOperario)).GetProperty("id").GetGuid();

        using var baja = await Cliente.PostAsync(new Uri($"/usuarios/{id}/baja", UriKind.Relative), null, Cancelacion);
        using var otraVez = await Cliente.PostAsync(new Uri($"/usuarios/{id}/baja", UriKind.Relative), null, Cancelacion);

        Assert.Equal(HttpStatusCode.OK, baja.StatusCode);
        Assert.False((await baja.LeerAsync<JsonElement>()).GetProperty("activo").GetBoolean());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, otraVez.StatusCode);
        Assert.Equal("El usuario ya está dado de baja", await DetalleAsync(otraVez));
        Assert.Equal(HttpStatusCode.Forbidden, await IngresarAsync("ana@campo.com", "segura-123"));
    }

    [Fact]
    public async Task El_unico_administrador_no_puede_darse_de_baja_ni_perder_el_rol()
    {
        var id = (await RegistrarAsync("admin@campo.com", DatosSemilla.IdRolAdministrador)).GetProperty("id").GetGuid();
        // The test user's role also manages roles; take that permission away so "admin" is the only administrator.
        using var sinAdminDePruebas = await Cliente.PutAsJsonAsync(
            $"/roles/{Fabrica.IdRolPruebas}/permisos",
            Permisos("usuarios_gestionar", "capturas_registrar"),
            Cancelacion);
        Assert.Equal(HttpStatusCode.OK, sinAdminDePruebas.StatusCode);

        using var baja = await Cliente.PostAsync(new Uri($"/usuarios/{id}/baja", UriKind.Relative), null, Cancelacion);
        using var rol = await Cliente.PutAsJsonAsync($"/usuarios/{id}/rol", new { id_rol = DatosSemilla.IdRolOperario }, Cancelacion);
        using var permisos = await Cliente.PutAsJsonAsync(
            $"/roles/{DatosSemilla.IdRolAdministrador}/permisos", Permisos("usuarios_gestionar"), Cancelacion);

        foreach (var respuesta in new[] { baja, rol })
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
            Assert.Equal("No puede quedar el sistema sin un administrador", await DetalleAsync(respuesta));
        }

        // The test session lost roles_gestionar, so it cannot edit role permissions anymore.
        Assert.Equal(HttpStatusCode.Forbidden, permisos.StatusCode);
    }

    [Fact]
    public async Task Los_roles_muestran_sus_permisos_y_el_catalogo_describe_cada_uno()
    {
        await RegistrarAsync("ana@campo.com", DatosSemilla.IdRolOperario);

        using var cambio = await Cliente.PutAsJsonAsync(
            $"/roles/{DatosSemilla.IdRolOperario}/permisos",
            Permisos("rodeo_consultar", "capturas_registrar"),
            Cancelacion);
        var roles = await Cliente.GetFromJsonAsync<JsonElement>("/roles", Cancelacion);
        var catalogo = await Cliente.GetFromJsonAsync<JsonElement>("/permisos", Cancelacion);

        var operario = (await cambio.LeerAsync<JsonElement>());
        Assert.Equal(["capturas_registrar", "rodeo_consultar"], operario.GetProperty("permisos").EnumerateArray().Select(p => p.GetString()));
        Assert.Equal(1, operario.GetProperty("cantidad_usuarios").GetInt32());
        Assert.Contains("Operario de Campo", roles.EnumerateArray().Select(r => r.GetProperty("nombre").GetString()));
        Assert.Equal(7, catalogo.GetArrayLength());
        Assert.Equal("roles_gestionar", catalogo.EnumerateArray().ElementAt(5).GetProperty("codigo").GetString());
        Assert.Equal("Gestionar roles y permisos", catalogo.EnumerateArray().ElementAt(5).GetProperty("descripcion").GetString());
    }

    [Fact]
    public async Task Sin_permiso_de_usuarios_no_se_gestionan_usuarios_ni_roles()
    {
        await RegistrarAsync("ana@campo.com", DatosSemilla.IdRolOperario);
        using var anonimo = Fabrica.CrearClienteAnonimo();
        using var login = await anonimo.PostAsJsonAsync("/auth/sesion", new { correo = "ana@campo.com", contrasena = "segura-123" }, Cancelacion);
        using var operario = Fabrica.CrearClienteConSesion(FabricaApi.LeerCookieDeSesion(login)!);

        using var usuarios = await operario.GetAsync(new Uri("/usuarios", UriKind.Relative), Cancelacion);
        using var roles = await operario.GetAsync(new Uri("/roles", UriKind.Relative), Cancelacion);
        using var permisos = await operario.PutAsJsonAsync(
            $"/roles/{DatosSemilla.IdRolOperario}/permisos", Permisos("roles_gestionar"), Cancelacion);

        Assert.Equal(HttpStatusCode.Forbidden, usuarios.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, roles.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, permisos.StatusCode);
    }
}
