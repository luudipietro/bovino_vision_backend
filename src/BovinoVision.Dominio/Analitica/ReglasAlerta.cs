namespace BovinoVision.Dominio.Analitica;

/// <summary>
/// Compara la GMD del intervalo de un pesaje con la GMD esperada de su lote:
/// una GMD negativa es una pérdida de peso; una GMD por debajo de una fracción del objetivo del lote es estancamiento.
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
