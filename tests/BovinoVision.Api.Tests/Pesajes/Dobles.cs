using BovinoVision.Aplicacion.Pesajes;

namespace BovinoVision.Api.Tests.Pesajes;

internal sealed class EstimadorControlado : IEstimadorPeso
{
    private int _llamadas;
    public int Llamadas => _llamadas;
    public bool Indisponible { get; set; }
    public ResultadoEstimacion Resultado { get; set; } = new(400m, "fake-v1");
    public Func<Task>? Esperar { get; set; }

    public async Task<ResultadoEstimacion> EstimarAsync(string referenciaImagen, CancellationToken cancelacion = default)
    {
        Interlocked.Increment(ref _llamadas);
        if (Esperar is { } esperar) { await esperar(); }
        if (Indisponible) { throw new HttpRequestException("offline"); }
        return Resultado;
    }
}

internal sealed class Reloj : TimeProvider
{
    public DateTimeOffset Ahora { get; set; } = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Ahora;
}
