namespace BovinoVision.Dominio.Analitica;

/// <summary>Umbrales ajustables de la regla de alerta (sección de configuración "Alertas").</summary>
public sealed record ParametrosReglasAlerta
{
    /// <summary>Hay estancamiento cuando la GMD del intervalo está por debajo de esta fracción del objetivo del lote.</summary>
    public decimal FraccionEstancamiento { get; init; } = 0.5m;

    public int PrioridadPerdida { get; init; } = 1;

    public int PrioridadEstancamiento { get; init; } = 2;
}
