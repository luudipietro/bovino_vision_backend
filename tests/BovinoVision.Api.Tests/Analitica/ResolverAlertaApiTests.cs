using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Establecimientos;
using BovinoVision.Dominio.Usuarios;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Api.Tests.Analitica;

/// <summary>POST /alertas/{id}/resolver: cierra una alerta activa del establecimiento de la sesión.</summary>
public class ResolverAlertaApiTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    private static readonly DateTimeOffset Inicio = DatosAnalitica.Inicio;

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private async Task<T> EnContextoAsync<T>(Func<BovinoVisionDbContext, Task<T>> accion)
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        return await accion(alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>());
    }

    private Task<Guid> CrearAlertaAsync(string identificador, Guid? idEstablecimiento = null) =>
        EnContextoAsync(async contexto =>
        {
            var datos = new DatosAnalitica(contexto);
            if (idEstablecimiento is { } id)
            {
                contexto.Establecimientos.Add(new Establecimiento(id, "Estancia ajena"));
            }

            var animal = datos.CrearAnimal(identificador, idEstablecimiento);
            datos.Pesar(animal, 300m, Inicio);
            var evento = datos.Pesar(animal, 280m, Inicio.AddDays(10));
            var alerta = datos.Alertar(evento, TipoAlerta.PerdidaPeso, 1, Inicio.AddDays(10));
            await datos.GuardarAsync();
            return alerta.Id;
        });

    private static Task<HttpResponseMessage> ResolverAsync(HttpClient cliente, Guid idAlerta) =>
        cliente.PostAsync(new Uri($"/alertas/{idAlerta}/resolver", UriKind.Relative), null, Cancelacion);

    private async Task<List<Guid>> IdsDeAlertasActivasAsync()
    {
        var alertas = await Cliente.GetFromJsonAsync<JsonElement>(new Uri("/alertas", UriKind.Relative), Cancelacion);
        return [.. alertas.EnumerateArray().Select(a => a.GetProperty("id").GetGuid())];
    }

    private async Task<HttpClient> ClienteConPermisosAsync(params Permiso[] permisos)
    {
        const string Correo = "solo-lectura@campo.com";
        const string Contrasena = "solo-lectura-123";
        await EnContextoAsync(async contexto =>
        {
            var hasher = Fabrica.Services.GetRequiredService<IHasherContrasenas>();
            var rol = new Rol(DatosSemilla.IdEstablecimiento, "Con permisos acotados", permisos);
            contexto.AddRange(rol, new Usuario(DatosSemilla.IdEstablecimiento, rol.Id, "Acotado", Correo, hasher.Generar(Contrasena)));
            await contexto.SaveChangesAsync(Cancelacion);
            return 0;
        });
        using var anonimo = Fabrica.CrearClienteAnonimo();
        using var login = await anonimo.PostAsJsonAsync("/auth/sesion", new { correo = Correo, contrasena = Contrasena }, Cancelacion);
        login.EnsureSuccessStatusCode();
        return Fabrica.CrearClienteConSesion(FabricaApi.LeerCookieDeSesion(login)!);
    }

    [Fact]
    public async Task Resolver_una_alerta_activa_responde_204_y_la_saca_de_las_alertas_activas()
    {
        var resuelta = await CrearAlertaAsync("AR-RES-01");
        var otra = await CrearAlertaAsync("AR-RES-02");

        using var respuesta = await ResolverAsync(Cliente, resuelta);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal([otra], await IdsDeAlertasActivasAsync());
        var estado = await EnContextoAsync(contexto => contexto.Alertas.Where(a => a.Id == resuelta).Select(a => a.Estado).SingleAsync(Cancelacion));
        Assert.Equal(EstadoAlerta.Resuelta, estado);
        var delAnimal = await Cliente.GetFromJsonAsync<JsonElement>(
            new Uri($"/animales/{await IdDeAnimalAsync("AR-RES-01")}/alertas", UriKind.Relative), Cancelacion);
        Assert.Equal("resuelta", Assert.Single(delAnimal.EnumerateArray()).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Resolver_dos_veces_la_misma_alerta_responde_409()
    {
        var idAlerta = await CrearAlertaAsync("AR-RES-03");
        using var primera = await ResolverAsync(Cliente, idAlerta);

        using var segunda = await ResolverAsync(Cliente, idAlerta);

        Assert.Equal(HttpStatusCode.NoContent, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        var cuerpo = await segunda.LeerAsync<JsonElement>();
        Assert.Equal("La alerta ya está resuelta", cuerpo.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Resolver_una_alerta_inexistente_responde_404()
    {
        using var respuesta = await ResolverAsync(Cliente, Guid.CreateVersion7());

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Resolver_una_alerta_de_otro_establecimiento_responde_404_y_no_la_modifica()
    {
        var idAlerta = await CrearAlertaAsync("AR-AJENO-01", Guid.CreateVersion7());

        using var respuesta = await ResolverAsync(Cliente, idAlerta);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var estado = await EnContextoAsync(contexto => contexto.Alertas.Where(a => a.Id == idAlerta).Select(a => a.Estado).SingleAsync(Cancelacion));
        Assert.Equal(EstadoAlerta.Activa, estado);
    }

    [Fact]
    public async Task Sin_rodeo_gestionar_responde_403_aunque_pueda_consultar_alertas()
    {
        var idAlerta = await CrearAlertaAsync("AR-RES-04");
        using var soloAnalitica = await ClienteConPermisosAsync(Permiso.AnaliticaConsultar);

        using var respuesta = await ResolverAsync(soloAnalitica, idAlerta);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal([idAlerta], await IdsDeAlertasActivasAsync());
    }

    [Fact]
    public async Task Sin_sesion_responde_401()
    {
        var idAlerta = await CrearAlertaAsync("AR-RES-05");
        using var anonimo = Fabrica.CrearClienteAnonimo();

        using var respuesta = await ResolverAsync(anonimo, idAlerta);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    private Task<Guid> IdDeAnimalAsync(string identificador) =>
        EnContextoAsync(contexto => contexto.Animales.Where(a => a.Identificador == identificador).Select(a => a.Id).SingleAsync(Cancelacion));
}
