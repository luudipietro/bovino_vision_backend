using System.Security.Cryptography;
using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Capturas;

public sealed record RegistrarCapturaComando(
    Guid IdSesion,
    Guid IdAnimal,
    string NombreArchivo,
    string TipoContenido,
    ReadOnlyMemory<byte> Contenido,
    DateTimeOffset? FechaCaptura = null);

/// <summary>Stores the photo of an animal; an invalid image is rejected before anything is stored.</summary>
public sealed class RegistrarCaptura(
    IRepositorioSesionesCaptura sesiones,
    IRepositorioCapturas capturas,
    IAnimalesDeCaptura animales,
    IAlmacenamientoImagenes almacenamiento,
    IUnidadDeTrabajo unidadDeTrabajo,
    IContextoUsuario usuario,
    TimeProvider tiempo)
{
    public const int TamanioMaximoBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> TiposDeImagenValidos =
        new(["image/jpeg", "image/png", "image/webp"], StringComparer.OrdinalIgnoreCase);

    public async Task<CapturaRespuesta> EjecutarAsync(RegistrarCapturaComando comando, CancellationToken cancelacion = default)
    {
        var sesion = await sesiones.ObtenerAsync(comando.IdSesion, cancelacion)
            ?? throw NoEncontradoError.De("la sesión de captura", comando.IdSesion);
        if (!await animales.ExisteAsync(comando.IdAnimal, usuario.IdEstablecimiento, cancelacion))
        {
            throw NoEncontradoError.De("el animal", comando.IdAnimal);
        }

        if (sesion.Estado == EstadoSesionCaptura.Finalizada)
        {
            throw new OperacionInvalidaError($"La sesión de captura {sesion.Id} está finalizada");
        }

        ValidarImagen(comando);

        var referencia = await almacenamiento.GuardarAsync(comando.NombreArchivo, comando.Contenido, cancelacion);
        try
        {
            var captura = new Captura(comando.IdAnimal, usuario.IdUsuario, sesion.Id, comando.FechaCaptura ?? tiempo.GetUtcNow());
            captura.AgregarImagen(referencia, CalcularHuella(comando.Contenido.Span), comando.TipoContenido);
            capturas.Agregar(captura);
            await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
            return CapturaRespuesta.Desde(captura, idEventoPesaje: null);
        }
        catch
        {
            // Nothing must stay stored when the capture could not be recorded.
            await almacenamiento.EliminarAsync(referencia, CancellationToken.None);
            throw;
        }
    }

    // Simplified quality check (type and size); real quality checks depend on the vision service.
    private static void ValidarImagen(RegistrarCapturaComando comando)
    {
        if (!TiposDeImagenValidos.Contains(comando.TipoContenido))
        {
            throw new OperacionInvalidaError("Formato de imagen no soportado");
        }

        if (comando.Contenido.IsEmpty)
        {
            throw new OperacionInvalidaError("El archivo está vacío");
        }

        if (comando.Contenido.Length > TamanioMaximoBytes)
        {
            throw new OperacionInvalidaError("La imagen supera el tamaño máximo permitido");
        }
    }

    private static string CalcularHuella(ReadOnlySpan<byte> contenido) =>
        Convert.ToHexStringLower(SHA256.HashData(contenido));
}
