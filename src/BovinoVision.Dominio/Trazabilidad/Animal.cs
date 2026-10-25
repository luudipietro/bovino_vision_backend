using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Trazabilidad;

public sealed class Animal : Entidad
{
    private readonly List<HistorialLote> _historialLotes = [];

    public Guid IdEstablecimiento { get; private set; }
    public string Identificador { get; private set; } = null!;
    public Guid? IdRaza { get; private set; }
    public Sexo? Sexo { get; private set; }
    public DateOnly? FechaNacimiento { get; private set; }

    public IReadOnlyCollection<HistorialLote> HistorialLotes => _historialLotes.AsReadOnly();

    public HistorialLote? AsignacionVigente => _historialLotes.SingleOrDefault(h => h.EstaVigente);

    private Animal() { }

    public Animal(Guid idEstablecimiento, string identificador, Guid? idRaza = null, Sexo? sexo = null, DateOnly? fechaNacimiento = null)
    {
        if (string.IsNullOrWhiteSpace(identificador))
        {
            throw new OperacionInvalidaError("El identificador del animal es obligatorio");
        }

        IdEstablecimiento = idEstablecimiento;
        Identificador = identificador.Trim();
        IdRaza = idRaza;
        Sexo = sexo;
        FechaNacimiento = fechaNacimiento;
    }

    /// <summary>Actualiza solo los valores provistos; los omitidos conservan su estado actual.</summary>
    public void ModificarDatos(Guid? idRaza, Sexo? sexo, DateOnly? fechaNacimiento)
    {
        IdRaza = idRaza ?? IdRaza;
        Sexo = sexo ?? Sexo;
        FechaNacimiento = fechaNacimiento ?? FechaNacimiento;
    }

    /// <summary>
    /// Cierra la asignación de lote vigente y abre una nueva. Devuelve false cuando el
    /// animal ya pertenece a ese lote, en cuyo caso no se registra nada.
    /// </summary>
    public bool AsignarLote(Guid idLote, Guid idUsuario, DateTimeOffset fecha)
    {
        var fechaUtc = fecha.ToUniversalTime();
        var vigente = AsignacionVigente;
        if (vigente?.IdLote == idLote)
        {
            return false;
        }

        vigente?.Cerrar(fechaUtc);
        _historialLotes.Add(new HistorialLote(Id, idLote, idUsuario, fechaUtc));
        return true;
    }

    public HistorialLote? AsignacionEn(DateTimeOffset fecha)
    {
        var fechaUtc = fecha.ToUniversalTime();
        return _historialLotes.SingleOrDefault(h => h.CubreFecha(fechaUtc));
    }
}
