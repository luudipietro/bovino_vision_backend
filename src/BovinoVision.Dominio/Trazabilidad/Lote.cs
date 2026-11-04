using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Trazabilidad;

public sealed class Lote : Entidad
{
    public Guid IdEstablecimiento { get; private set; }
    public string Codigo { get; private set; } = null!;
    public string? Nombre { get; private set; }

    /// <summary>Ganancia media diaria esperada (kg/día) que usa la alerta de estancamiento.</summary>
    public decimal? GmdObjetivo { get; private set; }

    private Lote() { }

    public Lote(Guid idEstablecimiento, string codigo, string? nombre = null, decimal? gmdObjetivo = null)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw new OperacionInvalidaError("El código del lote es obligatorio");
        }

        IdEstablecimiento = idEstablecimiento;
        Codigo = codigo.Trim();
        Nombre = nombre;
        DefinirGmdObjetivo(gmdObjetivo);
    }

    public void DefinirGmdObjetivo(decimal? gmdObjetivo)
    {
        if (gmdObjetivo <= 0)
        {
            throw new OperacionInvalidaError("La GMD objetivo debe ser positiva");
        }

        GmdObjetivo = gmdObjetivo;
    }
}
