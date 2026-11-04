using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Capturas;

public sealed class Captura : Entidad
{
    private readonly List<Imagen> _imagenes = [];

    public Guid IdAnimal { get; private set; }
    public Guid IdUsuario { get; private set; }
    public Guid IdSesion { get; private set; }

    /// <summary>Momento en que se tomó la foto; pasa a ser la fecha del pesaje aunque el procesamiento ocurra después.</summary>
    public DateTimeOffset FechaCaptura { get; private set; }

    public EstadoCaptura Estado { get; private set; }
    public string? MotivoFallo { get; private set; }
    public int Intentos { get; private set; }
    /// <summary>Fecha más temprana de reintento mientras está pendiente.</summary>
    public DateTimeOffset? ProximoIntento { get; private set; }

    /// <summary>Vencimiento de la concesión mientras se procesa; después, otro worker puede recuperar la captura.</summary>
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

    /// <summary>El operario confirma el envío de la captura a estimación (CU-14 paso 1).</summary>
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

    /// <summary>El estimador respondió pero no pudo estimar; no se crea ningún evento de pesaje.</summary>
    public void MarcarFallida(string motivo)
    {
        ExigirEstado(EstadoCaptura.Procesando);
        Estado = EstadoCaptura.Fallida;
        MotivoFallo = motivo;
        VenceProcesamiento = null;
    }

    /// <summary>El estimador no estaba disponible; la captura espera otro intento.</summary>
    public void ReintentarDespues(string motivo, DateTimeOffset proximoIntento)
    {
        ExigirEstado(EstadoCaptura.Procesando);
        Estado = EstadoCaptura.Pendiente;
        MotivoFallo = motivo;
        ProximoIntento = proximoIntento.ToUniversalTime();
        VenceProcesamiento = null;
    }

    /// <summary>El operario confirma el resultado (CU-15); nunca crea ni modifica el evento.</summary>
    public void RegistrarRevision(DateTimeOffset fecha)
    {
        ExigirEstado(EstadoCaptura.Procesada);
        FechaRevision ??= fecha.ToUniversalTime();
    }

    /// <summary>
    /// El operario quita la foto antes de pedir una estimación. Solo se puede descartar una captura
    /// registrada; las referencias devueltas le permiten al llamador borrar los archivos almacenados.
    /// </summary>
    public IReadOnlyList<string> Descartar()
    {
        if (Estado != EstadoCaptura.Registrada)
        {
            throw new OperacionInvalidaError($"Solo se puede descartar una captura registrada; la captura {Id} está en estado {Estado}");
        }

        return _imagenes.ConvertAll(i => i.ReferenciaImagen);
    }

    private void ExigirEstado(EstadoCaptura esperado)
    {
        if (Estado != esperado)
        {
            throw new OperacionInvalidaError($"La captura {Id} está en estado {Estado} y no {esperado}");
        }
    }
}
