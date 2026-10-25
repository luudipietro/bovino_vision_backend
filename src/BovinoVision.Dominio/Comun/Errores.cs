namespace BovinoVision.Dominio.Comun;

/// <summary>Violación de una regla de negocio que la API traduce a una respuesta HTTP.</summary>
public abstract class ErrorDeDominio(string mensaje) : Exception(mensaje);

public sealed class NoEncontradoError(string mensaje) : ErrorDeDominio(mensaje)
{
    public static NoEncontradoError De(string entidad, Guid id) => new($"No existe {entidad} {id}");
}

public sealed class ConflictoError(string mensaje) : ErrorDeDominio(mensaje);

public sealed class OperacionInvalidaError(string mensaje) : ErrorDeDominio(mensaje);

/// <summary>Credenciales o sesión faltantes o inválidas (401).</summary>
public sealed class NoAutenticadoError(string mensaje) : ErrorDeDominio(mensaje);

/// <summary>Autenticado pero sin permiso (403).</summary>
public sealed class AccesoDenegadoError(string mensaje) : ErrorDeDominio(mensaje);

/// <summary>Demasiados logins fallidos (429); la API envía el tiempo restante en Retry-After.</summary>
public sealed class UsuarioBloqueadoError(TimeSpan restante)
    : ErrorDeDominio($"El usuario está bloqueado por intentos fallidos. Volvé a intentar en {Math.Max(1, (int)Math.Ceiling(restante.TotalMinutes))} minutos.")
{
    public TimeSpan Restante { get; } = restante;
}
