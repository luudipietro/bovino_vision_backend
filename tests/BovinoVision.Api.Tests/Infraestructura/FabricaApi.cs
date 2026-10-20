using System.Collections.Concurrent;
using BovinoVision.Infraestructura;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private readonly bool _procesarEnSegundoPlano;
    private readonly RegistroInferencia _registro = new();

    public FabricaApi() : this(false) { }

    internal FabricaApi(bool procesarEnSegundoPlano) => _procesarEnSegundoPlano = procesarEnSegundoPlano;

    internal IReadOnlyCollection<Exception> ErroresInferencia => _registro.Errores.ToArray();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        // Program applies migrations before RunAsync starts any hosted consumer.
        _ = Services;
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
        builder.UseSetting("BaseDeDatos:AplicarMigracionesAlIniciar", "true");
        // Derived hosts explicitly opt in; a shared host must never claim their jobs.
        builder.UseSetting("Inferencia:ProcesarEnSegundoPlano", _procesarEnSegundoPlano.ToString());
        builder.ConfigureLogging(logging => logging.AddProvider(_registro));
    }

    private sealed class RegistroInferencia : ILoggerProvider
    {
        public ConcurrentQueue<Exception> Errores { get; } = new();

        public ILogger CreateLogger(string categoryName) => new RegistroCategoria(
            categoryName == "BovinoVision.Infraestructura.Pesajes.ServicioInferencia" ? Errores : null);

        public void Dispose() { }

        private sealed class RegistroCategoria(ConcurrentQueue<Exception>? errores) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => errores is not null && logLevel >= LogLevel.Error;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel) && exception is not null) { errores!.Enqueue(exception); }
            }
        }
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
