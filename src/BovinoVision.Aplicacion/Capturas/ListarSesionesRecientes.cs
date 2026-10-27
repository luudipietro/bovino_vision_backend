using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Capturas;

/// <param name="Limite">Cuántas sesiones devolver; <see cref="ListarSesionesRecientes.LimitePorDefecto"/> cuando es null.</param>
/// <param name="Desplazamiento">Cuántas de las más nuevas saltear, para paginar el historial; 0 cuando es null.</param>
/// <param name="Desde">Inicio inclusivo del rango por fecha de inicio de sesión.</param>
/// <param name="Hasta">Fin exclusivo del rango; ambos extremos son opcionales.</param>
public sealed record ListarSesionesRecientesConsulta(
    int? Limite = null, int? Desplazamiento = null, DateTimeOffset? Desde = null, DateTimeOffset? Hasta = null);

/// <summary>Las últimas sesiones de captura del usuario de la solicitud, de la más reciente a la más antigua, con sus cantidades de capturas.</summary>
public sealed class ListarSesionesRecientes(IConsultasCapturas consultas, IContextoUsuario usuario)
{
    public const int LimitePorDefecto = 5;
    public const int LimiteMaximo = 20;

    public async Task<IReadOnlyList<SesionRecienteRespuesta>> EjecutarAsync(
        ListarSesionesRecientesConsulta consulta,
        CancellationToken cancelacion = default)
    {
        var limite = consulta.Limite ?? LimitePorDefecto;
        if (limite is < 1 or > LimiteMaximo)
        {
            throw new OperacionInvalidaError($"El parámetro limite debe estar entre 1 y {LimiteMaximo}");
        }

        var desplazamiento = consulta.Desplazamiento ?? 0;
        if (desplazamiento < 0)
        {
            throw new OperacionInvalidaError("El parámetro desplazamiento no puede ser negativo");
        }

        if (consulta.Desde is { } desde && consulta.Hasta is { } hasta && desde >= hasta)
        {
            throw new OperacionInvalidaError("El parámetro desde debe ser anterior a hasta");
        }

        return await consultas.ListarSesionesRecientesAsync(
            usuario.IdUsuario, limite, desplazamiento,
            consulta.Desde?.ToUniversalTime(), consulta.Hasta?.ToUniversalTime(), cancelacion);
    }
}
