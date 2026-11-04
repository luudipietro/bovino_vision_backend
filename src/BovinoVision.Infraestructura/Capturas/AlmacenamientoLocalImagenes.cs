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

/// <summary>Escribe las imágenes en un directorio local; con un bucket configurado lo reemplaza <see cref="AlmacenamientoS3Imagenes"/>.</summary>
internal sealed class AlmacenamientoLocalImagenes(IOptions<OpcionesAlmacenamiento> opciones, IHostEnvironment entorno)
    : IAlmacenamientoImagenes
{
    private readonly string _directorio = Path.GetFullPath(opciones.Value.DirectorioImagenes, entorno.ContentRootPath);

    public async Task<string> GuardarAsync(string nombreSugerido, ReadOnlyMemory<byte> contenido, CancellationToken cancelacion = default)
    {
        Directory.CreateDirectory(_directorio);
        var referencia = ReferenciasDeImagen.Nueva(nombreSugerido);
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
        if (!ReferenciasDeImagen.EsNombreDeArchivo(referencia))
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
}
