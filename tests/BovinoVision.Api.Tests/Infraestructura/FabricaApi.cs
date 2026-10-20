using BovinoVision.Infraestructura;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace BovinoVision.Api.Tests.Infraestructura;

/// <summary>
/// Runs the real API against a disposable PostgreSQL container with all migrations applied.
/// Shared by every integration test through <see cref="ColeccionApi"/>.
/// </summary>
public sealed class FabricaApi : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Tables without seed data, emptied between tests (seeded catalogs stay).
    private const string SqlLimpieza =
        "TRUNCATE alertas, eventos_pesaje, imagenes, capturas, sesiones_captura, historial_lotes, animales, lotes CASCADE";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await Services.AplicarMigracionesAsync();
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public async Task LimpiarBaseAsync()
    {
        await using var alcance = Services.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
        await contexto.Database.ExecuteSqlRawAsync(SqlLimpieza);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{ExtensionesDeServicios.NombreConexion}", _postgres.GetConnectionString());
        builder.UseSetting("BaseDeDatos:AplicarMigracionesAlIniciar", "false");
    }
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionApi : ICollectionFixture<FabricaApi>
{
    public const string Nombre = "Api";
}

/// <summary>Base class for integration tests: fresh data before each test and a shared client.</summary>
[Collection(ColeccionApi.Nombre)]
public abstract class PruebaDeApi(FabricaApi fabrica) : IAsyncLifetime
{
    protected FabricaApi Fabrica { get; } = fabrica;

    protected HttpClient Cliente { get; } = fabrica.CreateClient();

    public virtual async ValueTask InitializeAsync() => await Fabrica.LimpiarBaseAsync();

    public virtual ValueTask DisposeAsync()
    {
        Cliente.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
