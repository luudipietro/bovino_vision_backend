using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BovinoVision.Api.Tests.Analitica;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Api.Tests.Trazabilidad;
using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Establecimientos;
using BovinoVision.Dominio.Usuarios;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Api.Tests.Regresion;

public class AlinearMockupsRegresionApiTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    private static readonly DateTimeOffset Inicio = DatosAnalitica.Inicio;
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private async Task<T> EnContextoAsync<T>(Func<BovinoVisionDbContext, Task<T>> accion)
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        return await accion(alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>());
    }

    private async Task<(HttpClient Cliente, Guid IdUsuario)> UsuarioConAsync(string correo, string nombre, params Permiso[] permisos)
    {
        var id = await EnContextoAsync(async contexto =>
        {
            var hasher = Fabrica.Services.GetRequiredService<IHasherContrasenas>();
            var rol = new Rol(DatosSemilla.IdEstablecimiento, "Rol " + nombre, permisos);
            var usuario = new Usuario(DatosSemilla.IdEstablecimiento, rol.Id, nombre, correo, hasher.Generar("clave-123456"));
            contexto.AddRange(rol, usuario);
            await contexto.SaveChangesAsync(Cancelacion);
            return usuario.Id;
        });
        using var anonimo = Fabrica.CrearClienteAnonimo();
        using var login = await anonimo.PostAsJsonAsync("/auth/sesion", new { correo, contrasena = "clave-123456" }, Cancelacion);
        login.EnsureSuccessStatusCode();
        return (Fabrica.CrearClienteConSesion(FabricaApi.LeerCookieDeSesion(login)!), id);
    }

    private Task<Guid> AlertaAsync(string ident, Guid? idEst = null, bool resuelta = false) => EnContextoAsync(async contexto =>
    {
        var datos = new DatosAnalitica(contexto);
        if (idEst is { } e) { contexto.Establecimientos.Add(new Establecimiento(e, "Ajena")); }
        var animal = datos.CrearAnimal(ident, idEst);
        datos.Pesar(animal, 300m, Inicio);
        var ev = datos.Pesar(animal, 280m, Inicio.AddDays(10));
        var alerta = datos.Alertar(ev, TipoAlerta.PerdidaPeso, 1, Inicio.AddDays(10));
        if (resuelta) { alerta.Resolver(); }
        await datos.GuardarAsync();
        return alerta.Id;
    });

    private Task<Dictionary<Guid, EstadoAlerta>> EstadosAsync() =>
        EnContextoAsync(c => c.Alertas.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Estado, Cancelacion));

    private static Task<HttpResponseMessage> Resolver(HttpClient c, Guid id) =>
        c.PostAsync(new Uri($"/alertas/{id}/resolver", UriKind.Relative), null, Cancelacion);

    [Fact]
    public async Task P1_resolver_requiere_exactamente_rodeo_gestionar_y_funciona_sin_analitica()
    {
        var id = await AlertaAsync("P1");
        var (soloGestionar, _) = await UsuarioConAsync("g@x.com", "G", Permiso.RodeoGestionar);
        var (todoMenos, _) = await UsuarioConAsync("t@x.com", "T", [.. Enum.GetValues<Permiso>().Where(p => p != Permiso.RodeoGestionar)]);
        var antes = await EstadosAsync();

        using var r403 = await Resolver(todoMenos, id);
        Assert.Equal(HttpStatusCode.Forbidden, r403.StatusCode);
        Assert.Equal(antes, await EstadosAsync());

        using var r204 = await Resolver(soloGestionar, id);
        Assert.Equal(HttpStatusCode.NoContent, r204.StatusCode);
        Assert.Equal(EstadoAlerta.Resuelta, (await EstadosAsync())[id]);
    }

    [Fact]
    public async Task P2_alerta_ajena_ya_resuelta_o_activa_da_404_nunca_409_y_no_cambia_nada()
    {
        var ajenaActiva = await AlertaAsync("P2a", Guid.CreateVersion7());
        var ajenaResuelta = await AlertaAsync("P2b", Guid.CreateVersion7(), resuelta: true);
        var antes = await EstadosAsync();

        using var r1 = await Resolver(Cliente, ajenaActiva);
        using var r2 = await Resolver(Cliente, ajenaResuelta);

        Assert.Equal(HttpStatusCode.NotFound, r1.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, r2.StatusCode);
        Assert.Equal(antes, await EstadosAsync());
    }

    [Fact]
    public async Task P3_rechazos_409_404_no_alteran_otras_alertas_y_rutas_raras()
    {
        var a = await AlertaAsync("P3a");
        var b = await AlertaAsync("P3b");
        using var ok = await Resolver(Cliente, a);
        var antes = await EstadosAsync();

        using var r409 = await Resolver(Cliente, a);
        using var r404 = await Resolver(Cliente, Guid.CreateVersion7());
        using var noGuid = await Cliente.PostAsync(new Uri("/alertas/abc/resolver", UriKind.Relative), null, Cancelacion);
        using var get = await Cliente.GetAsync(new Uri($"/alertas/{b}/resolver", UriKind.Relative), Cancelacion);
        using var anonimo = Fabrica.CrearClienteAnonimo();
        using var r401 = await Resolver(anonimo, b);

        Assert.Equal(HttpStatusCode.Conflict, r409.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, r404.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noGuid.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, r401.StatusCode);
        Assert.Equal(antes, await EstadosAsync());
        Assert.Equal(EstadoAlerta.Activa, antes[b]);
    }

    [Fact]
    public async Task P4_resolver_concurrente_deja_un_204_y_ningun_500()
    {
        var id = await AlertaAsync("P4");
        var tareas = Enumerable.Range(0, 6).Select(async _ => { using var r = await Resolver(Cliente, id); return r.StatusCode; });
        var codigos = await Task.WhenAll(tareas);
        Console.WriteLine("P4 codes: " + string.Join(",", codigos));
        Assert.DoesNotContain(HttpStatusCode.InternalServerError, codigos);
        Assert.Contains(HttpStatusCode.NoContent, codigos);
        Assert.Equal(EstadoAlerta.Resuelta, (await EstadosAsync())[id]);
    }

    [Fact]
    public async Task P5_sesiones_solo_del_usuario_logueado_y_dos_usuarios_no_se_mezclan()
    {
        var (ana, _) = await UsuarioConAsync("ana@x.com", "Ana", Permiso.CapturasRegistrar);
        var (beto, _) = await UsuarioConAsync("beto@x.com", "Beto", Permiso.CapturasRegistrar);
        var (solo, _) = await UsuarioConAsync("s@x.com", "Sin", Permiso.RodeoConsultar);
        using var sa1 = await ana.PostAsync(new Uri("/sesiones-captura", UriKind.Relative), null, Cancelacion);
        using var sb1 = await beto.PostAsync(new Uri("/sesiones-captura", UriKind.Relative), null, Cancelacion);
        using var sb2 = await beto.PostAsync(new Uri("/sesiones-captura", UriKind.Relative), null, Cancelacion);
        Assert.Equal(HttpStatusCode.Created, sa1.StatusCode);
        var idA = (await sa1.LeerAsync<JsonElement>()).GetProperty("id").GetGuid();
        var idsB = new[] { (await sb1.LeerAsync<JsonElement>()).GetProperty("id").GetGuid(), (await sb2.LeerAsync<JsonElement>()).GetProperty("id").GetGuid() };

        var deA = await ana.GetFromJsonAsync<JsonElement>(new Uri("/sesiones-captura?limite=20", UriKind.Relative), Cancelacion);
        var deB = await beto.GetFromJsonAsync<JsonElement>(new Uri("/sesiones-captura", UriKind.Relative), Cancelacion);
        using var rSolo = await solo.GetAsync(new Uri("/sesiones-captura", UriKind.Relative), Cancelacion);

        Assert.Equal([idA], deA.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()));
        Assert.Equal(idsB.OrderBy(x => x), deB.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).OrderBy(x => x));
        Assert.Equal(HttpStatusCode.Forbidden, rSolo.StatusCode);
    }

    [Theory]
    [InlineData("1.5", 400)]
    [InlineData("99999999999", 400)]
    [InlineData("abc", 400)]
    [InlineData("", -1)]
    [InlineData("1", 200)]
    [InlineData("20", 200)]
    [InlineData("21", 422)]
    [InlineData("0", 422)]
    [InlineData("-5", 422)]
    [InlineData("2147483647", 422)]
    public async Task P6_limite(string valor, int esperado)
    {
        using var r = await Cliente.GetAsync(new Uri($"/sesiones-captura?limite={valor}", UriKind.Relative), Cancelacion);
        Console.WriteLine($"P6 limite='{valor}' -> {(int)r.StatusCode}");
        if (esperado > 0) { Assert.Equal(esperado, (int)r.StatusCode); }
        else { Assert.True((int)r.StatusCode is 200 or 400 or 422); }
    }

    [Fact]
    public async Task P7_autenticacion_sigue_rechazando_expirada_revocada_dada_de_baja_y_token_falso()
    {
        var (a, idA) = await UsuarioConAsync("a@x.com", "A", Permiso.CapturasRegistrar);
        var (b, _) = await UsuarioConAsync("b@x.com", "B", Permiso.CapturasRegistrar);
        var (c, idC) = await UsuarioConAsync("c@x.com", "C", Permiso.CapturasRegistrar);
        using var vigente = await a.GetAsync(new Uri("/auth/sesion", UriKind.Relative), Cancelacion);
        Assert.Equal(HttpStatusCode.OK, vigente.StatusCode);

        await EnContextoAsync(async ctx =>
        {
            await ctx.SesionesUsuario.Where(s => s.IdUsuario == idA)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiraEn, DateTimeOffset.UtcNow.AddMinutes(-1)), Cancelacion);
            return 0;
        });
        using var salir = await b.DeleteAsync(new Uri("/auth/sesion", UriKind.Relative), Cancelacion);
        using var baja = await Cliente.PostAsync(new Uri($"/usuarios/{idC}/baja", UriKind.Relative), null, Cancelacion);
        Assert.True(baja.IsSuccessStatusCode);

        foreach (var cl in new[] { a, b, c })
        {
            using var r = await cl.GetAsync(new Uri("/auth/sesion", UriKind.Relative), Cancelacion);
            using var r2 = await cl.GetAsync(new Uri("/sesiones-captura", UriKind.Relative), Cancelacion);
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, r2.StatusCode);
        }

        using var falso = Fabrica.CrearClienteConSesion("token-falso-0000");
        using var rf = await falso.GetAsync(new Uri("/auth/sesion", UriKind.Relative), Cancelacion);
        Assert.Equal(HttpStatusCode.Unauthorized, rf.StatusCode);
    }

    [Fact]
    public async Task P8_ultimo_acceso_por_usuario_no_se_filtra_entre_usuarios()
    {
        var (a, idA) = await UsuarioConAsync("a8@x.com", "A8", Permiso.CapturasRegistrar);
        await Task.Delay(30, Cancelacion);
        var (b, idB) = await UsuarioConAsync("b8@x.com", "B8", Permiso.CapturasRegistrar);
        await Task.Delay(30, Cancelacion);
        using var anon = Fabrica.CrearClienteAnonimo();
        using var otroLoginB = await anon.PostAsJsonAsync("/auth/sesion", new { correo = "b8@x.com", contrasena = "clave-123456" }, Cancelacion);

        var sa = (await a.GetFromJsonAsync<JsonElement>(new Uri("/auth/sesion", UriKind.Relative), Cancelacion)).GetProperty("usuario");
        var sb = (await b.GetFromJsonAsync<JsonElement>(new Uri("/auth/sesion", UriKind.Relative), Cancelacion)).GetProperty("usuario");
        var lista = await Cliente.GetFromJsonAsync<JsonElement>(new Uri("/usuarios", UriKind.Relative), Cancelacion);
        var enBase = await EnContextoAsync(c => c.SesionesUsuario.GroupBy(s => s.IdUsuario)
            .Select(g => new { Id = g.Key, M = g.Max(s => s.CreadaEn) }).ToDictionaryAsync(x => x.Id, x => x.M, Cancelacion));

        Assert.Equal(enBase[idA], sa.GetProperty("ultimo_acceso").GetDateTimeOffset());
        Assert.Equal(enBase[idB], sb.GetProperty("ultimo_acceso").GetDateTimeOffset());
        foreach (var u in lista.EnumerateArray())
        {
            var id = u.GetProperty("id").GetGuid();
            if (enBase.TryGetValue(id, out var esperado)) { Assert.Equal(esperado, u.GetProperty("ultimo_acceso").GetDateTimeOffset()); }
            else { Assert.Equal(JsonValueKind.Null, u.GetProperty("ultimo_acceso").ValueKind); }
        }

        Assert.NotEqual(sa.GetProperty("ultimo_acceso").GetDateTimeOffset(), sb.GetProperty("ultimo_acceso").GetDateTimeOffset());
    }

    [Fact]
    public async Task P9_gmd_del_listado_coincide_con_el_ultimo_del_historial_con_cinco_pesajes()
    {
        var animal = await Cliente.CrearAnimalAsync("P9");
        var fechas = new[] { 0, 7, 20, 20, 33 };
        var pesos = new[] { 300m, 310m, 330m, 331m, 350m };
        for (var i = 0; i < fechas.Length; i++) { await Fabrica.RegistrarPesajeAsync(animal.Id, pesos[i], Inicio.AddDays(fechas[i])); }

        var lista = await Cliente.LeerOkAsync<JsonElement>("/animales");
        var ficha = await Cliente.LeerOkAsync<JsonElement>($"/animales/{animal.Id}");
        var hist = await Cliente.LeerOkAsync<JsonElement>($"/animales/{animal.Id}/pesajes");
        Console.WriteLine("P9 hist: " + hist);

        Assert.Equal(1.46m, lista[0].GetProperty("gmd_kg_dia").GetDecimal());
        Assert.Equal(1.46m, ficha.GetProperty("gmd_kg_dia").GetDecimal());
        Assert.Equal(hist[0].GetProperty("gmd_kg_dia").GetDecimal(), ficha.GetProperty("gmd_kg_dia").GetDecimal());
    }

    [Fact]
    public async Task P10_gmd_con_filtros_del_listado_y_animal_sin_pesajes()
    {
        var lote = await Cliente.CrearLoteAsync("P10", 0.5m);
        var a = await Cliente.CrearAnimalAsync("P10A", lote.Id);
        var b = await Cliente.CrearAnimalAsync("P10B");
        await Fabrica.RegistrarPesajeAsync(a.Id, 300m, Inicio);
        await Fabrica.RegistrarPesajeAsync(a.Id, 290m, Inicio.AddDays(5), EstadoAlerta.Activa);
        await Fabrica.RegistrarPesajeAsync(b.Id, 200m, Inicio);

        var porLote = await Cliente.LeerOkAsync<JsonElement>($"/animales?id_lote={lote.Id}");
        var todos = await Cliente.LeerOkAsync<JsonElement>("/animales");
        Console.WriteLine("P10 todos: " + todos);
        Assert.Equal(-2.0m, porLote[0].GetProperty("gmd_kg_dia").GetDecimal());
        Assert.Equal(2, todos.GetArrayLength());
        Assert.Equal(JsonValueKind.Null, todos[1].GetProperty("gmd_kg_dia").ValueKind);
    }
}
