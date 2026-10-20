namespace BovinoVision.Dominio.Analitica;

/// <summary>
/// Compares the interval GMD of a weighing with the expected GMD of its lot:
/// a negative GMD is a weight loss; a GMD below a fraction of the lot objective is stagnation.
/// </summary>
public sealed class ReglasAlerta
{
    public const string Version = "gmd-v1";

    private readonly ParametrosReglasAlerta _parametros;

    public ReglasAlerta(ParametrosReglasAlerta parametros)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parametros.FraccionEstancamiento);
        _parametros = parametros;
    }

    public Alerta? Evaluar(Guid idEvento, decimal gmd, decimal? gmdObjetivo, DateTimeOffset fechaGeneracion)
    {
        if (gmd < 0)
        {
            return new Alerta(idEvento, TipoAlerta.PerdidaPeso, _parametros.PrioridadPerdida, gmd, gmdObjetivo, Version, fechaGeneracion);
        }

        if (gmdObjetivo is { } objetivo && gmd < _parametros.FraccionEstancamiento * objetivo)
        {
            return new Alerta(idEvento, TipoAlerta.Estancamiento, _parametros.PrioridadEstancamiento, gmd, objetivo, Version, fechaGeneracion);
        }

        return null;
    }
}
