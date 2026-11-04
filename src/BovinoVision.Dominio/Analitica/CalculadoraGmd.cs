namespace BovinoVision.Dominio.Analitica;

/// <summary>Un peso observado en un momento (la hora de captura de un evento de pesaje).</summary>
public readonly record struct Medicion(decimal PesoKg, DateTimeOffset FechaPesaje);

/// <summary>
/// Ganancia media diaria (GMD, kg/día) entre pesajes. Los días se cuentan entre fechas
/// calendario en la zona horaria del establecimiento, así que la hora de cada captura no importa.
/// </summary>
public sealed class CalculadoraGmd(TimeZoneInfo zonaHoraria)
{
    public TimeZoneInfo ZonaHoraria { get; } = zonaHoraria;

    public DateOnly FechaLocal(DateTimeOffset instante) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, ZonaHoraria).DateTime);

    /// <summary>Primer instante de una fecha calendario local, en UTC.</summary>
    public DateTimeOffset InicioDelDia(DateOnly fecha) =>
        new(TimeZoneInfo.ConvertTimeToUtc(fecha.ToDateTime(TimeOnly.MinValue), ZonaHoraria), TimeSpan.Zero);

    /// <summary>Null cuando ambos pesajes caen en la misma fecha o el segundo no es posterior.</summary>
    public decimal? Calcular(Medicion anterior, Medicion siguiente)
    {
        var dias = FechaLocal(siguiente.FechaPesaje).DayNumber - FechaLocal(anterior.FechaPesaje).DayNumber;
        if (dias <= 0)
        {
            return null;
        }

        return Math.Round((siguiente.PesoKg - anterior.PesoKg) / dias, 2, MidpointRounding.ToEven);
    }

    /// <summary>GMD de cada par de pesajes consecutivos, ordenados por fecha; se omiten los pares del mismo día.</summary>
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

    /// <summary>Promedio redondeado a un decimal; null cuando no hay valores.</summary>
    public static decimal? Promediar(IReadOnlyCollection<decimal> valores) =>
        valores.Count == 0 ? null : Math.Round(valores.Average(), 1, MidpointRounding.ToEven);
}
