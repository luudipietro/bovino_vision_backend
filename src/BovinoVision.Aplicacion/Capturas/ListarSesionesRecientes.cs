using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Capturas;

/// <param name="Limite">How many sessions to return; <see cref="ListarSesionesRecientes.LimitePorDefecto"/> when null.</param>
public sealed record ListarSesionesRecientesConsulta(int? Limite = null);

/// <summary>The latest capture sessions of the user of the request, newest first, with their capture counts.</summary>
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
