using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Trazabilidad;

/// <summary>Asignación de un animal a un lote durante un período; abierta mientras VigenteHasta es null.</summary>
public sealed class HistorialLote : Entidad
{
    public Guid IdAnimal { get; private set; }
    public Guid IdLote { get; private set; }
    public Guid IdUsuario { get; private set; }
    public DateTimeOffset VigenteDesde { get; private set; }
    public DateTimeOffset? VigenteHasta { get; private set; }

    private HistorialLote() { }

    internal HistorialLote(Guid idAnimal, Guid idLote, Guid idUsuario, DateTimeOffset vigenteDesde)
    {
        IdAnimal = idAnimal;
        IdLote = idLote;
        IdUsuario = idUsuario;
        VigenteDesde = vigenteDesde;
    }

    public bool EstaVigente => VigenteHasta is null;

    public bool CubreFecha(DateTimeOffset fecha) =>
        VigenteDesde <= fecha && (VigenteHasta is null || fecha < VigenteHasta);

    internal void Cerrar(DateTimeOffset fecha)
    {
        if (fecha < VigenteDesde)
        {
            throw new OperacionInvalidaError("La asignación de lote no puede cerrarse antes de su inicio");
        }

        VigenteHasta = fecha;
    }
}
