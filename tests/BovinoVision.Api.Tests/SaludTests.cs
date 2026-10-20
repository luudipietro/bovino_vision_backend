using System.Net;
using System.Text.Json;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Api.Tests;

public class SaludTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    [Fact]
    public async Task Health_devuelve_ok()
    {
        var respuesta = await Cliente.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.LeerAsync<JsonElement>();
        Assert.Equal("ok", cuerpo.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Las_migraciones_cargan_la_semilla_del_establecimiento_usuario_y_razas()
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
        var cancelacion = TestContext.Current.CancellationToken;

        Assert.True(await contexto.Establecimientos.AnyAsync(e => e.Id == DatosSemilla.IdEstablecimiento, cancelacion));
        Assert.True(await contexto.Usuarios.AnyAsync(u => u.Id == DatosSemilla.IdUsuarioSistema, cancelacion));
        Assert.Equal(DatosSemilla.Razas.Count, await contexto.Razas.CountAsync(cancelacion));
    }
}
