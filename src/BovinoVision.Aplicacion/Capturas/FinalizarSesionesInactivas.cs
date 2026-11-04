using BovinoVision.Aplicacion.Comun;

namespace BovinoVision.Aplicacion.Capturas;

public sealed class OpcionesSesionesCaptura
{
    public const string Seccion = "SesionesCaptura";

    public bool FinalizarInactivasEnSegundoPlano { get; set; } = true;

    /// <summary>Horas sin capturas tras las cuales una sesión abierta se da por abandonada.</summary>
    public int HorasInactividad { get; set; } = 6;

    public int IntervaloMinutos { get; set; } = 15;
}

/// <summary>
/// Cierra las sesiones que el cliente no llegó a finalizar (pestaña cerrada, red caída, celular apagado). La fecha de
/// fin es la última actividad de la sesión, no la hora del cierre automático.
/// </summary>
public sealed class FinalizarSesionesInactivas(
    IRepositorioSesionesCaptura sesiones,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider tiempo,
    OpcionesSesionesCaptura opciones)
{
    public async Task<int> EjecutarAsync(CancellationToken cancelacion = default)
    {
        var limite = tiempo.GetUtcNow().AddHours(-opciones.HorasInactividad);
        var inactivas = await sesiones.ListarInactivasAsync(limite, cancelacion);
        if (inactivas.Count == 0)
        {
            return 0;
        }

        foreach (var inactiva in inactivas)
        {
            inactiva.Sesion.Finalizar(inactiva.UltimaActividad);
        }

        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        return inactivas.Count;
    }
}
