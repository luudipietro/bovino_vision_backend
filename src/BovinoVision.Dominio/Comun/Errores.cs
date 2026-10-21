namespace BovinoVision.Dominio.Comun;

/// <summary>Business rule violation that the API translates into an HTTP response.</summary>
public abstract class ErrorDeDominio(string mensaje) : Exception(mensaje);

public sealed class NoEncontradoError(string mensaje) : ErrorDeDominio(mensaje)
{
    public static NoEncontradoError De(string entidad, Guid id) => new($"No existe {entidad} {id}");
}

public sealed class ConflictoError(string mensaje) : ErrorDeDominio(mensaje);

public sealed class OperacionInvalidaError(string mensaje) : ErrorDeDominio(mensaje);

/// <summary>Missing or invalid credentials or session (401).</summary>
public sealed class NoAutenticadoError(string mensaje) : ErrorDeDominio(mensaje);

/// <summary>Authenticated but not allowed (403).</summary>
public sealed class AccesoDenegadoError(string mensaje) : ErrorDeDominio(mensaje);

/// <summary>Too many failed logins (429); the API sends the remaining time as Retry-After.</summary>
public sealed class UsuarioBloqueadoError(TimeSpan restante)
    : ErrorDeDominio($"El usuario está bloqueado por intentos fallidos. Volvé a intentar en {Math.Max(1, (int)Math.Ceiling(restante.TotalMinutes))} minutos.")
{
    public TimeSpan Restante { get; } = restante;
}
