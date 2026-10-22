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

    public Task<Stream?> AbrirAsync(string referencia, CancellationToken cancelacion = default)
    {
        var ruta = RutaDentroDelDirectorio(referencia);
        if (ruta is null || !File.Exists(ruta))
        {
            return Task.FromResult<Stream?>(null);
        }

        try
        {
            return Task.FromResult<Stream?>(new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // The file was removed between the existence check and the open.
            return Task.FromResult<Stream?>(null);
        }
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

    // The stored reference could have been tampered with, so the resolved path must stay inside the directory.
    // The separator in the prefix keeps a sibling such as "uploads-otro" from passing as "uploads".
    private string? RutaDentroDelDirectorio(string referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia))
        {
            return null;
        }

        string ruta;
        try
        {
            ruta = Path.GetFullPath(referencia, _directorio);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var prefijo = Path.TrimEndingDirectorySeparator(_directorio) + Path.DirectorySeparatorChar;
        var comparacion = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return ruta.StartsWith(prefijo, comparacion) ? ruta : null;
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
