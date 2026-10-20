using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace BovinoVision.Api.Tests.Infraestructura;

/// <summary>Migrations that move existing data run against their own database, from the previous schema.</summary>
public sealed class MigracionesTests : IAsyncLifetime
{
    private const string MigracionInicial = "20261020003057_Inicial";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task El_vencimiento_de_una_captura_procesando_pasa_a_su_propia_columna()
    {
        var vencimiento = new DateTimeOffset(2026, 10, 20, 12, 2, 0, TimeSpan.Zero);
        var reintento = new DateTimeOffset(2026, 10, 20, 12, 5, 0, TimeSpan.Zero);
        var procesando = Guid.CreateVersion7();
        var pendiente = Guid.CreateVersion7();
        await using var contexto = CrearContexto();
        var migrador = contexto.GetService<IMigrator>();
        await migrador.MigrateAsync(MigracionInicial, TestContext.Current.CancellationToken);
        await InsertarCapturaAsync(procesando, "procesando", vencimiento);
        await InsertarCapturaAsync(pendiente, "pendiente", reintento);

        await migrador.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal((null, vencimiento), await LeerFechasAsync(procesando));
        Assert.Equal((reintento, null), await LeerFechasAsync(pendiente));
    }

    private BovinoVisionDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<BovinoVisionDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    private async Task InsertarCapturaAsync(Guid id, string estado, DateTimeOffset proximoIntento)
    {
        await using var conexion = new NpgsqlConnection(_postgres.GetConnectionString());
        await conexion.OpenAsync(TestContext.Current.CancellationToken);
        // Foreign keys are irrelevant to the data move, so the parent rows are skipped.
        await using var comando = new NpgsqlCommand("""
            SET session_replication_role = replica;
            INSERT INTO capturas (id, id_animal, id_usuario, id_sesion, fecha_captura, estado, intentos, proximo_intento)
            VALUES (@id, @id, @id, @id, @fecha, @estado, 1, @proximo);
            """, conexion);
        comando.Parameters.AddWithValue("id", id);
        comando.Parameters.AddWithValue("fecha", proximoIntento.AddDays(-1));
        comando.Parameters.AddWithValue("estado", estado);
        comando.Parameters.AddWithValue("proximo", proximoIntento);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<(DateTimeOffset? ProximoIntento, DateTimeOffset? VenceProcesamiento)> LeerFechasAsync(Guid id)
    {
        await using var conexion = new NpgsqlConnection(_postgres.GetConnectionString());
        await conexion.OpenAsync(TestContext.Current.CancellationToken);
        await using var comando = new NpgsqlCommand(
            "SELECT proximo_intento, vence_procesamiento FROM capturas WHERE id = @id", conexion);
        comando.Parameters.AddWithValue("id", id);
        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        await lector.ReadAsync(TestContext.Current.CancellationToken);
        return (Leer(lector, 0), Leer(lector, 1));
    }

    private static DateTimeOffset? Leer(NpgsqlDataReader lector, int columna) =>
        lector.IsDBNull(columna) ? null : lector.GetFieldValue<DateTimeOffset>(columna);
}
