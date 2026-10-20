namespace BovinoVision.Dominio.Comun;

/// <summary>Business rule violation that the API translates into an HTTP response.</summary>
public abstract class ErrorDeDominio(string mensaje) : Exception(mensaje);

public sealed class NoEncontradoError(string mensaje) : ErrorDeDominio(mensaje)
{
    public static NoEncontradoError De(string entidad, Guid id) => new($"No existe {entidad} {id}");
}

public sealed class ConflictoError(string mensaje) : ErrorDeDominio(mensaje);

public sealed class OperacionInvalidaError(string mensaje) : ErrorDeDominio(mensaje);
