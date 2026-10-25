using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Analitica;

public sealed class Alerta : Entidad
{
    public Guid IdEvento { get; private set; }
    public TipoAlerta Tipo { get; private set; }

    /// <summary>1 is the most urgent.</summary>
    public int Prioridad { get; private set; }

    /// <summary>Average daily gain (kg/day) of the interval that triggered the alert.</summary>
    public decimal Variacion { get; private set; }

    public decimal? GmdEsperada { get; private set; }
    public string VersionRegla { get; private set; } = null!;
    public EstadoAlerta Estado { get; private set; }
    public DateTimeOffset FechaGeneracion { get; private set; }

    private Alerta() { }

    public Alerta(
        Guid idEvento,
        TipoAlerta tipo,
        int prioridad,
        decimal variacion,
        decimal? gmdEsperada,
        string versionRegla,
        DateTimeOffset fechaGeneracion)
    {
        if (prioridad < 1)
        {
            throw new OperacionInvalidaError("La prioridad de una alerta empieza en 1");
        }

        IdEvento = idEvento;
        Tipo = tipo;
        Prioridad = prioridad;
        Variacion = variacion;
        GmdEsperada = gmdEsperada;
        VersionRegla = versionRegla;
        Estado = EstadoAlerta.Activa;
        FechaGeneracion = fechaGeneracion.ToUniversalTime();
    }

    /// <summary>Closes an active alert; an alert that is already resolved cannot be resolved again.</summary>
    public void Resolver()
    {
        if (Estado == EstadoAlerta.Resuelta)
        {
            throw new ConflictoError("La alerta ya está resuelta");
        }

        Estado = EstadoAlerta.Resuelta;
    }
}

public enum TipoAlerta
{
    PerdidaPeso,
    Estancamiento,
}

public enum EstadoAlerta
{
    Activa,
    Resuelta,
}
