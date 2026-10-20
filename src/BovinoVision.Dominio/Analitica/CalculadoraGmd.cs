namespace BovinoVision.Dominio.Analitica;

/// <summary>A weight observed at a moment (the capture time of a weighing event).</summary>
public readonly record struct Medicion(decimal PesoKg, DateTimeOffset FechaPesaje);

/// <summary>
/// Average daily gain (GMD, kg/day) between weighings. Days are counted between calendar
/// dates in the establecimiento's time zone, so the hour of each capture does not matter.
/// </summary>
public sealed class CalculadoraGmd(TimeZoneInfo zonaHoraria)
{
    public TimeZoneInfo ZonaHoraria { get; } = zonaHoraria;

    public DateOnly FechaLocal(DateTimeOffset instante) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, ZonaHoraria).DateTime);

    /// <summary>First instant of a local calendar date, in UTC.</summary>
    public DateTimeOffset InicioDelDia(DateOnly fecha) =>
        new(TimeZoneInfo.ConvertTimeToUtc(fecha.ToDateTime(TimeOnly.MinValue), ZonaHoraria), TimeSpan.Zero);

    /// <summary>Null when both weighings fall on the same date or the second one is not later.</summary>
    public decimal? Calcular(Medicion anterior, Medicion siguiente)
    {
        var dias = FechaLocal(siguiente.FechaPesaje).DayNumber - FechaLocal(anterior.FechaPesaje).DayNumber;
        if (dias <= 0)
        {
            return null;
        }

        return Math.Round((siguiente.PesoKg - anterior.PesoKg) / dias, 2, MidpointRounding.ToEven);
    }

    /// <summary>GMD of every pair of consecutive weighings, ordered by date; same-day pairs are skipped.</summary>
    public IReadOnlyList<decimal> GmdEntreConsecutivos(IEnumerable<Medicion> mediciones)
    {
        var ordenadas = mediciones.OrderBy(m => m.FechaPesaje).ToList();
        var gmds = new List<decimal>();
        for (var i = 1; i < ordenadas.Count; i++)
        {
            if (Calcular(ordenadas[i - 1], ordenadas[i]) is { } gmd)
            {
                gmds.Add(gmd);
            }
        }

        return gmds;
    }

    /// <summary>Average rounded to one decimal; null when there are no values.</summary>
    public static decimal? Promediar(IReadOnlyCollection<decimal> valores) =>
        valores.Count == 0 ? null : Math.Round(valores.Average(), 1, MidpointRounding.ToEven);
}
