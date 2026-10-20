using System.Text.RegularExpressions;
using BovinoVision.Aplicacion.Capturas;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BovinoVision.Infraestructura.Capturas;

public sealed class OpcionesAlmacenamiento
{
    public const string Seccion = "Almacenamiento";

    /// <summary>Relative paths are resolved against the application content root.</summary>
    public string DirectorioImagenes { get; set; } = "uploads";
}

/// <summary>Writes images to a local directory; a bucket adapter replaces it in production.</summary>
internal sealed partial class AlmacenamientoLocalImagenes(IOptions<OpcionesAlmacenamiento> opciones, IHostEnvironment entorno)
    : IAlmacenamientoImagenes
{
    private const string ExtensionPorDefecto = ".jpg";

    private readonly string _directorio = Path.GetFullPath(opciones.Value.DirectorioImagenes, entorno.ContentRootPath);

    public async Task<string> GuardarAsync(string nombreSugerido, ReadOnlyMemory<byte> contenido, CancellationToken cancelacion = default)
    {
        Directory.CreateDirectory(_directorio);
        var referencia = $"{Guid.CreateVersion7():N}{ExtensionSegura(nombreSugerido)}";
        await File.WriteAllBytesAsync(Path.Combine(_directorio, referencia), contenido, cancelacion);
        return referencia;
    }

    public Task EliminarAsync(string referencia, CancellationToken cancelacion = default)
    {
        if (Path.GetFileName(referencia) != referencia)
        {
            throw new ArgumentException($"Referencia de imagen inválida: {referencia}", nameof(referencia));
        }

        File.Delete(Path.Combine(_directorio, referencia));
        return Task.CompletedTask;
    }

    // The client chooses the file name, so only short alphanumeric extensions are kept.
    private static string ExtensionSegura(string nombreSugerido)
    {
        var extension = Path.GetExtension(nombreSugerido);
        return ExtensionValida().IsMatch(extension) ? extension.ToLowerInvariant() : ExtensionPorDefecto;
    }

    [GeneratedRegex(@"^\.[A-Za-z0-9]{1,5}$")]
    private static partial Regex ExtensionValida();
}
