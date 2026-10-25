using System.Text.RegularExpressions;
using BovinoVision.Aplicacion.Capturas;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BovinoVision.Infraestructura.Capturas;

public sealed class OpcionesAlmacenamiento
{
    public const string Seccion = "Almacenamiento";

    /// <summary>Las rutas relativas se resuelven respecto del directorio raíz de contenido de la aplicación.</summary>
    public string DirectorioImagenes { get; set; } = "uploads";
}

/// <summary>Escribe las imágenes en un directorio local; en producción lo reemplaza un adaptador de bucket.</summary>
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
            // FileShare.Delete permite descartar una captura mientras se descarga su foto.
            return Task.FromResult<Stream?>(new FileStream(
                ruta, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, bufferSize: 4096, useAsync: true));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // El archivo se borró entre la verificación de existencia y la apertura.
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

    // La referencia guardada podría estar adulterada, así que la ruta resuelta debe quedar dentro del directorio.
    // El separador en el prefijo evita que un hermano como "uploads-otro" pase por "uploads".
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

    // El cliente elige el nombre del archivo, así que solo se conservan extensiones alfanuméricas cortas.
    private static string ExtensionSegura(string nombreSugerido)
    {
        var extension = Path.GetExtension(nombreSugerido);
        return ExtensionValida().IsMatch(extension) ? extension.ToLowerInvariant() : ExtensionPorDefecto;
    }

    [GeneratedRegex(@"^\.[A-Za-z0-9]{1,5}$")]
    private static partial Regex ExtensionValida();
}
