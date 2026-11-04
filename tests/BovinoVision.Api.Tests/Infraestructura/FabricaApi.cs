using System.Collections.Concurrent;
using System.Net.Http.Json;
using BovinoVision.Aplicacion.Usuarios;
using BovinoVision.Dominio.Usuarios;
using BovinoVision.Infraestructura;
using BovinoVision.Infraestructura.Persistencia;
using BovinoVision.Infraestructura.Seguridad;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace BovinoVision.Api.Tests.Infraestructura;

/// <summary>
/// Ejecuta la API real contra un contenedor PostgreSQL descartable con todas las migraciones aplicadas.
/// La comparten todos los tests de integración mediante <see cref="ColeccionApi"/>. Los clientes inician sesión
/// como un usuario de prueba cuyo rol tiene todos los permisos, salvo que se creen con <see cref="CrearClienteAnonimo"/>.
/// </summary>
public sealed class FabricaApi : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Tablas sin datos de semilla, que se vacían entre tests (los catálogos de semilla se conservan).
    private const string SqlLimpieza =
        "TRUNCATE alertas, eventos_pesaje, imagenes, capturas, sesiones_captura, historial_lotes, animales, lotes CASCADE";

    public const string CorreoPruebas = "pruebas@bovinovision.test";
    public const string ContrasenaPruebas = "contrasena-de-pruebas";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly bool _procesarEnSegundoPlano;
    private readonly RegistroInferencia _registro = new();

    public FabricaApi() : this(false) { }

    internal FabricaApi(bool procesarEnSegundoPlano) => _procesarEnSegundoPlano = procesarEnSegundoPlano;

    internal IReadOnlyCollection<Exception> ErroresInferencia => _registro.Errores.ToArray();

    public Guid IdUsuarioPruebas { get; private set; }

    public Guid IdRolPruebas { get; private set; }

    private string? _token;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        // Program aplica las migraciones antes de que RunAsync inicie cualquier consumidor alojado.
        _ = Services;
        await CrearUsuarioDePruebasAsync();
        using var anonimo = CrearClienteAnonimo();
        using var respuesta = await anonimo.PostAsJsonAsync(
            "/auth/sesion", new { correo = CorreoPruebas, contrasena = ContrasenaPruebas });
        respuesta.EnsureSuccessStatusCode();
        _token = LeerCookieDeSesion(respuesta) ?? throw new InvalidOperationException("El login de pruebas no devolvió la cookie");
    }

    /// <summary>Cliente sin la sesión de prueba, para tests de login y autorización.</summary>
    public HttpClient CrearClienteAnonimo()
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Remove("Cookie");
        return cliente;
    }

    /// <summary>Cliente con sesión iniciada con el token dado.</summary>
    public HttpClient CrearClienteConSesion(string token)
    {
        var cliente = CrearClienteAnonimo();
        cliente.DefaultRequestHeaders.Add("Cookie", $"{SesionHttp.Cookie}={token}");
        return cliente;
    }

    public static string? LeerCookieDeSesion(HttpResponseMessage respuesta) =>
        respuesta.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Select(c => c.Split(';')[0])
                .Where(c => c.StartsWith($"{SesionHttp.Cookie}=", StringComparison.Ordinal))
                .Select(c => c[(SesionHttp.Cookie.Length + 1)..])
                .FirstOrDefault(valor => valor.Length > 0)
            : null;

    // Todos los clientes, incluidos los de hosts derivados, usan HTTPS (la cookie de sesión es
    // Secure) y llevan la sesión de prueba una vez que existe.
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.BaseAddress = new Uri("https://localhost");
        if (_token is not null)
        {
            client.DefaultRequestHeaders.Add("Cookie", $"{SesionHttp.Cookie}={_token}");
        }
    }

    private async Task CrearUsuarioDePruebasAsync()
    {
        await using var alcance = Services.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
        var hasher = alcance.ServiceProvider.GetRequiredService<IHasherContrasenas>();
        var rol = new Rol(DatosSemilla.IdEstablecimiento, "Pruebas", Enum.GetValues<Permiso>());
        var usuario = new Usuario(DatosSemilla.IdEstablecimiento, rol.Id, "Usuario de pruebas", CorreoPruebas, hasher.Generar(ContrasenaPruebas));
        contexto.AddRange(rol, usuario);
        await contexto.SaveChangesAsync();
        (IdRolPruebas, IdUsuarioPruebas) = (rol.Id, usuario.Id);
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
        // Los usuarios, roles y sesiones creados por un test se eliminan; los roles de semilla y de prueba recuperan sus permisos.
        await contexto.SesionesUsuario.Where(s => s.IdUsuario != IdUsuarioPruebas).ExecuteDeleteAsync();
        await contexto.Usuarios.Where(u => u.Id != IdUsuarioPruebas && u.Id != DatosSemilla.IdUsuarioSistema).ExecuteDeleteAsync();
        var conservados = DatosSemilla.Roles.Select(r => r.Id).Append(IdRolPruebas).ToArray();
        await contexto.Roles.Where(r => !conservados.Contains(r.Id)).ExecuteDeleteAsync();
        foreach (var (id, _, permisos) in DatosSemilla.Roles)
        {
            IReadOnlyList<Permiso> originales = permisos;
            await contexto.Roles.Where(r => r.Id == id).ExecuteUpdateAsync(r => r.SetProperty(x => x.Permisos, originales));
        }

        IReadOnlyList<Permiso> todos = Enum.GetValues<Permiso>();
        await contexto.Roles.Where(r => r.Id == IdRolPruebas).ExecuteUpdateAsync(r => r.SetProperty(x => x.Permisos, todos));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{ExtensionesDeServicios.NombreConexion}", _postgres.GetConnectionString());
        builder.UseSetting("BaseDeDatos:AplicarMigracionesAlIniciar", "true");
        // Lo bastante larga para que los hosts con reloj falso nunca vean vencer la sesión de prueba.
        builder.UseSetting("Autenticacion:DuracionSesionHoras", "87600");
        // Los hosts derivados lo habilitan explícitamente; un host compartido nunca debe tomar sus trabajos.
        builder.UseSetting("Inferencia:ProcesarEnSegundoPlano", _procesarEnSegundoPlano.ToString());
        // Un servicio de estimación configurado en la máquina no debe alcanzar a los tests.
        builder.UseSetting("Estimador:UrlBase", "");
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

/// <summary>Clase base de los tests de integración: datos limpios antes de cada test y un cliente compartido.</summary>
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
