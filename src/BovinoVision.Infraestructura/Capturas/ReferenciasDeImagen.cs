using System.Text.RegularExpressions;

namespace BovinoVision.Infraestructura.Capturas;

/// <summary>Formato de las referencias de imagen, compartido por todos los adaptadores de almacenamiento.</summary>
internal static partial class ReferenciasDeImagen
{
    private const string ExtensionPorDefecto = ".jpg";

    /// <summary>Un GUID v7 con la extensión del nombre sugerido, o ".jpg" si no es segura.</summary>
    public static string Nueva(string nombreSugerido) => $"{Guid.CreateVersion7():N}{ExtensionSegura(nombreSugerido)}";

    /// <summary>Una referencia es un nombre de archivo sin directorios; cualquier otra cosa está adulterada.</summary>
    public static bool EsNombreDeArchivo(string referencia) =>
        !string.IsNullOrWhiteSpace(referencia) && Path.GetFileName(referencia) == referencia && referencia is not ("." or "..");

    // El cliente elige el nombre del archivo, así que solo se conservan extensiones alfanuméricas cortas.
    private static string ExtensionSegura(string nombreSugerido)
    {
        var extension = Path.GetExtension(nombreSugerido);
        return ExtensionValida().IsMatch(extension) ? extension.ToLowerInvariant() : ExtensionPorDefecto;
    }

    [GeneratedRegex(@"^\.[A-Za-z0-9]{1,5}$")]
    private static partial Regex ExtensionValida();
}
