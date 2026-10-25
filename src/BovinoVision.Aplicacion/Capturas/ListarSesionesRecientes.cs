using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Capturas;

/// <param name="Limite">Cuántas sesiones devolver; <see cref="ListarSesionesRecientes.LimitePorDefecto"/> cuando es null.</param>
public sealed record ListarSesionesRecientesConsulta(int? Limite = null);

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

        return await consultas.ListarSesionesRecientesAsync(usuario.IdUsuario, limite, cancelacion);
    }
}
