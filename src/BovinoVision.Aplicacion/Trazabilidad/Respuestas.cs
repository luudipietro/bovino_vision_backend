using BovinoVision.Dominio.Trazabilidad;

namespace BovinoVision.Aplicacion.Trazabilidad;

public sealed record RazaRespuesta(Guid Id, string Nombre);

public sealed record LoteRespuesta(Guid Id, string Codigo, string? Nombre, decimal? GmdObjetivo, int CantidadAnimales);

/// <summary>
/// Animal tal como se muestra en la lista del rodeo y en su ficha. <paramref name="GmdKgDia"/> es la GMD entre sus dos últimos
/// pesajes, nunca se guarda; <paramref name="IdImagenUltimoPesaje"/> es la foto que usó su último pesaje, null si nunca se pesó.
/// </summary>
public sealed record AnimalRespuesta(
    Guid Id,
    string Identificador,
    Guid IdEstablecimiento,
    Guid? IdLote,
    string? LoteCodigo,
    Guid? IdRaza,
    string? Raza,
    Sexo? Sexo,
    DateOnly? FechaNacimiento,
    decimal? UltimoPesoKg,
    int AlertasActivas,
    decimal? GmdKgDia,
    Guid? IdImagenUltimoPesaje);

public sealed record AsignacionLoteRespuesta(
    Guid IdLote,
    string LoteCodigo,
    DateTimeOffset VigenteDesde,
    DateTimeOffset? VigenteHasta);
