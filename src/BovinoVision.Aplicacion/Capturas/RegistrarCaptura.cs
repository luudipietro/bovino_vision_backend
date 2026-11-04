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

/// <summary>Guarda la foto de un animal; una imagen inválida se rechaza antes de almacenar nada.</summary>
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

    /// <summary>Desfase de reloj permitido del dispositivo que tomó la foto.</summary>
    public static readonly TimeSpan ToleranciaRelojCliente = TimeSpan.FromMinutes(5);

    private static readonly HashSet<string> TiposDeImagenValidos =
        new(["image/jpeg", "image/png"], StringComparer.OrdinalIgnoreCase);

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
        var ahora = tiempo.GetUtcNow();
        if (comando.FechaCaptura > ahora + ToleranciaRelojCliente)
        {
            throw new OperacionInvalidaError("La fecha de captura no puede ser posterior al momento actual");
        }

        var referencia = await almacenamiento.GuardarAsync(comando.NombreArchivo, comando.Contenido, cancelacion);
        try
        {
            var captura = new Captura(comando.IdAnimal, usuario.IdUsuario, sesion.Id, comando.FechaCaptura ?? ahora);
            captura.AgregarImagen(referencia, CalcularHuella(comando.Contenido.Span), comando.TipoContenido);
            capturas.Agregar(captura);
            await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
            return CapturaRespuesta.Desde(captura, idEventoPesaje: null);
        }
        catch
        {
            // No debe quedar nada almacenado cuando no se pudo registrar la captura.
            await almacenamiento.EliminarAsync(referencia, CancellationToken.None);
            throw;
        }
    }

    // Control de calidad simplificado (tipo y tamaño); los controles de calidad reales dependen del servicio de visión.
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
