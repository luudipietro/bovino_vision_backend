using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Capturas;

public sealed class Captura : Entidad
{
    private readonly List<Imagen> _imagenes = [];

    public Guid IdAnimal { get; private set; }
    public Guid IdUsuario { get; private set; }
    public Guid IdSesion { get; private set; }

    /// <summary>Moment the photo was taken; it becomes the weighing date even if processing happens later.</summary>
    public DateTimeOffset FechaCaptura { get; private set; }

    public EstadoCaptura Estado { get; private set; }
    public string? MotivoFallo { get; private set; }
    public int Intentos { get; private set; }
    /// <summary>Earliest retry date while pending.</summary>
    public DateTimeOffset? ProximoIntento { get; private set; }

    /// <summary>Lease deadline while processing; after it another worker may reclaim the capture.</summary>
    public DateTimeOffset? VenceProcesamiento { get; private set; }
    public DateTimeOffset? FechaRevision { get; private set; }

    public IReadOnlyCollection<Imagen> Imagenes => _imagenes.AsReadOnly();

    private Captura() { }

    public Captura(Guid idAnimal, Guid idUsuario, Guid idSesion, DateTimeOffset fechaCaptura)
    {
        IdAnimal = idAnimal;
        IdUsuario = idUsuario;
        IdSesion = idSesion;
        FechaCaptura = fechaCaptura.ToUniversalTime();
        Estado = EstadoCaptura.Registrada;
    }

    public Imagen AgregarImagen(string referenciaImagen, string huellaVerificacion, string tipoContenido)
    {
        if (Estado != EstadoCaptura.Registrada)
        {
            throw new OperacionInvalidaError("Solo se pueden agregar imágenes a una captura registrada");
        }

        var imagen = new Imagen(Id, referenciaImagen, huellaVerificacion, tipoContenido);
        _imagenes.Add(imagen);
        return imagen;
    }

    /// <summary>Operator confirms sending the capture to estimation (CU-14 step 1).</summary>
    public void SolicitarEstimacion()
    {
        switch (Estado)
        {
            case EstadoCaptura.Registrada when _imagenes.Count == 0:
                throw new OperacionInvalidaError($"La captura {Id} no tiene imágenes");
            case EstadoCaptura.Registrada:
                Estado = EstadoCaptura.Pendiente;
                return;
            case EstadoCaptura.Pendiente or EstadoCaptura.Procesando:
                throw new ConflictoError($"La captura {Id} ya está en proceso de estimación");
            case EstadoCaptura.Procesada:
                throw new ConflictoError($"La captura {Id} ya tiene un pesaje asociado");
            default:
                throw new OperacionInvalidaError($"La estimación de la captura {Id} falló; registre una nueva captura");
        }
    }

    public void IniciarProcesamiento(DateTimeOffset? vencimiento = null)
    {
        ExigirEstado(EstadoCaptura.Pendiente);
        Estado = EstadoCaptura.Procesando;
        Intentos++;
        ProximoIntento = null;
        VenceProcesamiento = vencimiento?.ToUniversalTime();
    }

    public void MarcarProcesada()
    {
        ExigirEstado(EstadoCaptura.Procesando);
        Estado = EstadoCaptura.Procesada;
        MotivoFallo = null;
        VenceProcesamiento = null;
    }

    /// <summary>The estimator answered but could not estimate; no weighing event is created.</summary>
    public void MarcarFallida(string motivo)
    {
        ExigirEstado(EstadoCaptura.Procesando);
        Estado = EstadoCaptura.Fallida;
        MotivoFallo = motivo;
        VenceProcesamiento = null;
    }

    /// <summary>The estimator was unavailable; the capture waits for another attempt.</summary>
    public void ReintentarDespues(string motivo, DateTimeOffset proximoIntento)
    {
        ExigirEstado(EstadoCaptura.Procesando);
        Estado = EstadoCaptura.Pendiente;
        MotivoFallo = motivo;
        ProximoIntento = proximoIntento.ToUniversalTime();
        VenceProcesamiento = null;
    }

    /// <summary>Operator acknowledges the result (CU-15); it never creates or changes the event.</summary>
    public void RegistrarRevision(DateTimeOffset fecha)
    {
        ExigirEstado(EstadoCaptura.Procesada);
        FechaRevision ??= fecha.ToUniversalTime();
    }

    private void ExigirEstado(EstadoCaptura esperado)
    {
        if (Estado != esperado)
        {
            throw new OperacionInvalidaError($"La captura {Id} está en estado {Estado} y no {esperado}");
        }
    }
}
