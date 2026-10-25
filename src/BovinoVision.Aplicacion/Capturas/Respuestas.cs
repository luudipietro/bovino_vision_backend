using BovinoVision.Dominio.Capturas;

namespace BovinoVision.Aplicacion.Capturas;

public sealed record SesionCapturaRespuesta(
    Guid Id,
    Guid IdUsuario,
    DateTimeOffset FechaInicio,
    DateTimeOffset? FechaFin,
    EstadoSesionCaptura Estado)
{
    public static SesionCapturaRespuesta Desde(SesionCaptura sesion) =>
        new(sesion.Id, sesion.IdUsuario, sesion.FechaInicio, sesion.FechaFin, sesion.Estado);
}

public sealed record CapturaRespuesta(
    Guid Id,
    Guid IdSesion,
    Guid IdAnimal,
    DateTimeOffset FechaCaptura,
    EstadoCaptura Estado,
    string? MotivoFallo,
    int Intentos,
    DateTimeOffset? FechaRevision,
    Guid? IdEventoPesaje)
{
    public static CapturaRespuesta Desde(Captura captura, Guid? idEventoPesaje) =>
        new(
            captura.Id,
            captura.IdSesion,
            captura.IdAnimal,
            captura.FechaCaptura,
            captura.Estado,
            captura.MotivoFallo,
            captura.Intentos,
            captura.FechaRevision,
            idEventoPesaje);
}

/// <summary>Gallery item of a capture session (CU-13).</summary>
public sealed record CapturaDeSesionRespuesta(
    Guid Id,
    Guid IdAnimal,
    string AnimalIdentificador,
    DateTimeOffset FechaCaptura,
    EstadoCaptura Estado,
    string? MotivoFallo,
    Guid? IdEventoPesaje,
    Guid? IdImagen);

/// <summary>Capture session listed in the recent sessions of a user.</summary>
public sealed record SesionRecienteRespuesta(
    Guid Id,
    DateTimeOffset FechaInicio,
    DateTimeOffset? FechaFin,
    EstadoSesionCaptura Estado,
    int CantidadCapturas,
    int CantidadProcesadas);
