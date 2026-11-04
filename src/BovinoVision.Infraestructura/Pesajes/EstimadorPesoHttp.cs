using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BovinoVision.Aplicacion.Capturas;
using BovinoVision.Aplicacion.Pesajes;
using Microsoft.Extensions.Options;

namespace BovinoVision.Infraestructura.Pesajes;

/// <summary>Patrón Options. Sección Estimador: sin <see cref="UrlBase"/> se registra el estimador simulado.</summary>
public sealed class OpcionesEstimador
{
    public const string Seccion = "Estimador";

    public string? UrlBase { get; set; }

    /// <summary>Token de servicio del estimador (`TOKEN_SERVICIO`); se configura por user secrets o variables de entorno.</summary>
    public string? Token { get; set; }

    public int IntervaloConsultaMilisegundos { get; set; } = 1000;
}

/// <summary>
/// Adapter del servicio de estimación (API REST asincrónica de `servicio-estimacion`): envía la foto por multipart y
/// consulta el trabajo hasta que termina. La concesión del procesador limita la espera total mediante la cancelación.
/// </summary>
internal sealed class EstimadorPesoHttp(HttpClient cliente, IAlmacenamientoImagenes almacenamiento, IOptions<OpcionesEstimador> opciones)
    : IEstimadorPeso
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private static readonly Dictionary<string, string> MensajesDeError = new()
    {
        ["formato_no_admitido"] = "El estimador no admite el formato de la imagen",
        ["imagen_corrupta"] = "La imagen está dañada y no se puede leer",
        ["imagen_muy_grande"] = "La imagen supera el tamaño máximo del estimador",
        ["resolucion_insuficiente"] = "La resolución de la foto es insuficiente para estimar el peso",
        ["no_autenticado"] = "El servicio de estimación rechazó las credenciales",
        ["cola_llena"] = "El servicio de estimación está saturado",
        ["trabajo_no_encontrado"] = "El trabajo de estimación venció en el servicio",
        ["modelo_no_disponible"] = "El modelo de estimación no está disponible",
        ["tiempo_excedido"] = "La estimación superó el tiempo máximo",
        ["referencia_inaccesible"] = "El servicio de estimación no pudo leer la imagen",
        ["error_interno"] = "El servicio de estimación falló por un error interno",
    };

    private static readonly Dictionary<string, string> MotivosNoConfiable = new()
    {
        ["sin_animal"] = "no se detectó ningún bovino",
        ["deteccion_dudosa"] = "la detección del animal es dudosa",
        ["partes_tapadas"] = "hay partes del animal tapadas",
        ["animal_cortado"] = "el animal sale cortado en la foto",
        ["imagen_borrosa"] = "la foto está borrosa",
        ["fuera_de_rango"] = "el peso estimado está fuera del rango válido del modelo",
    };

    public async Task<ResultadoEstimacion> EstimarAsync(TrabajoInferencia trabajo, CancellationToken cancelacion = default)
    {
        try
        {
            return await SolicitarAsync(trabajo, cancelacion);
        }
        catch (JsonException)
        {
            return Fallo("El servicio de estimación devolvió una respuesta inválida", reintentable: true);
        }
    }

    private async Task<ResultadoEstimacion> SolicitarAsync(TrabajoInferencia trabajo, CancellationToken cancelacion)
    {
        var imagen = await almacenamiento.AbrirAsync(trabajo.ReferenciaImagen, cancelacion);
        if (imagen is null) { return Fallo("La imagen de la captura ya no está disponible"); }

        using var pedido = new HttpRequestMessage(HttpMethod.Post, "v1/estimaciones") { Content = Formulario(trabajo, imagen) };
        // El servicio devuelve el mismo trabajo (aunque haya fallado) si se repite la clave, así que cada intento usa una propia.
        pedido.Headers.Add("Idempotency-Key", $"{trabajo.IdCaptura:N}-{trabajo.Intento}");
        using var aceptado = await cliente.SendAsync(pedido, cancelacion);
        if (!aceptado.IsSuccessStatusCode) { return await RechazoAsync(aceptado, cancelacion); }
        var id = (await LeerAsync<TrabajoAceptado>(aceptado, cancelacion)).Id;

        while (true)
        {
            using var consulta = await cliente.GetAsync($"v1/estimaciones/{Uri.EscapeDataString(id)}", cancelacion);
            if (!consulta.IsSuccessStatusCode) { return await RechazoAsync(consulta, cancelacion); }
            var estado = await LeerAsync<EstadoTrabajo>(consulta, cancelacion);
            if (estado.Estado == "completado") { return Completado(estado); }
            if (estado.Estado == "fallido")
            {
                return Fallo(Mensaje(estado.Codigo, estado.Detalle) ?? "La estimación falló en el servicio", estado.Reintentable);
            }
            await Task.Delay(TimeSpan.FromMilliseconds(opciones.Value.IntervaloConsultaMilisegundos), cancelacion);
        }
    }

    private static MultipartFormDataContent Formulario(TrabajoInferencia trabajo, Stream imagen)
    {
        var archivo = new StreamContent(imagen);
        archivo.Headers.ContentType = new MediaTypeHeaderValue(trabajo.TipoContenido);
        archivo.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"imagenes\"",
            FileName = $"\"{trabajo.ReferenciaImagen}\"",
        };
        return new MultipartFormDataContent { archivo };
    }

    /// <summary>Solo un peso confiable registra el pesaje; el resto termina en falla con los motivos del modelo.</summary>
    private static ResultadoEstimacion Completado(EstadoTrabajo estado)
    {
        if (estado.Confiable && estado.PesoKg is { } peso) { return new(Math.Round(peso, 2), estado.VersionModelo ?? ""); }

        var motivos = (estado.Motivos ?? []).Select(m => MotivosNoConfiable.GetValueOrDefault(m, m)).ToList();
        const string Encabezado = "No se pudo estimar el peso con confianza";
        return Fallo(motivos.Count == 0 ? Encabezado : $"{Encabezado}: {string.Join("; ", motivos)}");
    }

    private static async Task<ResultadoEstimacion> RechazoAsync(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        Problema? problema = null;
        try { problema = await respuesta.Content.ReadFromJsonAsync<Problema>(Json, cancelacion); }
        catch (JsonException) { }

        var codigoHttp = (int)respuesta.StatusCode;
        // Credenciales, saturación, vencimiento y fallas del servidor pueden resolverse; un pedido inválido no cambia al repetirlo.
        var reintentable = codigoHttp >= 500 || respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            or HttpStatusCode.NotFound or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;
        var motivo = Mensaje(problema?.Codigo, problema?.Detail) ?? $"El servicio de estimación rechazó el pedido (HTTP {codigoHttp})";
        return Fallo(motivo, reintentable);
    }

    private static string? Mensaje(string? codigo, string? detalle) =>
        codigo is not null && MensajesDeError.TryGetValue(codigo, out var mensaje) ? mensaje : detalle;

    private static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta, CancellationToken cancelacion) =>
        await respuesta.Content.ReadFromJsonAsync<T>(Json, cancelacion) ?? throw new JsonException("Respuesta vacía del servicio de estimación");

    private static ResultadoEstimacion Fallo(string motivo, bool reintentable = false) => new(null, "", motivo, reintentable);

    private sealed record TrabajoAceptado(string Id);

    private sealed record EstadoTrabajo(
        string Estado, decimal? PesoKg, bool Confiable, string[]? Motivos, string? VersionModelo, string? Codigo, string? Detalle, bool Reintentable);

    private sealed record Problema(string? Codigo, string? Detail);
}
