using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BovinoVision.Api.Tests.Infraestructura;

/// <summary>Same JSON conventions as the API: snake_case properties and enums.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static Task<T?> LeerAsync<T>(this HttpResponseMessage respuesta) =>
        respuesta.Content.ReadFromJsonAsync<T>(Opciones);

    public static Task<HttpResponseMessage> EnviarJsonAsync<T>(this HttpClient cliente, string ruta, T cuerpo) =>
        cliente.PostAsJsonAsync(ruta, cuerpo, Opciones);

    public static Task<HttpResponseMessage> ReemplazarJsonAsync<T>(this HttpClient cliente, string ruta, T cuerpo) =>
        cliente.PutAsJsonAsync(ruta, cuerpo, Opciones);
}
