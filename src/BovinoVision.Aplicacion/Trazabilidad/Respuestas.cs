using BovinoVision.Dominio.Trazabilidad;

namespace BovinoVision.Aplicacion.Trazabilidad;

public sealed record RazaRespuesta(Guid Id, string Nombre);

public sealed record LoteRespuesta(Guid Id, string Codigo, string? Nombre, decimal? GmdObjetivo, int CantidadAnimales);

/// <summary>Animal as shown in the herd list and in its record (ficha). <paramref name="GmdKgDia"/> is the GMD between its last two weighings, never stored.</summary>
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
    decimal? GmdKgDia);

public sealed record AsignacionLoteRespuesta(
    Guid IdLote,
    string LoteCodigo,
    DateTimeOffset VigenteDesde,
    DateTimeOffset? VigenteHasta);
