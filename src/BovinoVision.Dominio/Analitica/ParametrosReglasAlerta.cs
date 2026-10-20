namespace BovinoVision.Dominio.Analitica;

/// <summary>Tunable thresholds of the alert rule (configuration section "Alertas").</summary>
public sealed record ParametrosReglasAlerta
{
    /// <summary>Stagnation when the interval GMD is below this fraction of the lot's objective.</summary>
    public decimal FraccionEstancamiento { get; init; } = 0.5m;

    public int PrioridadPerdida { get; init; } = 1;

    public int PrioridadEstancamiento { get; init; } = 2;
}
