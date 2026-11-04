using BovinoVision.Dominio.Capturas;

namespace BovinoVision.Aplicacion.Capturas;

/// <summary>Una sesión activa y su última actividad: la última captura, o el inicio si no tiene capturas.</summary>
public sealed record SesionInactiva(SesionCaptura Sesion, DateTimeOffset UltimaActividad);

public interface IRepositorioSesionesCaptura
{
    Task<SesionCaptura?> ObtenerAsync(Guid id, CancellationToken cancelacion = default);

    void Agregar(SesionCaptura sesion);

    /// <summary>Las sesiones activas cuya última actividad es anterior a <paramref name="sinActividadDesde"/>.</summary>
    Task<IReadOnlyList<SesionInactiva>> ListarInactivasAsync(DateTimeOffset sinActividadDesde, CancellationToken cancelacion = default);
}
