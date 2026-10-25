using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Analitica;

public sealed record ResolverAlertaComando(Guid IdAlerta);

/// <summary>Cierra una alerta activa del establecimiento; deja de aparecer entre las activas.</summary>
public sealed class ResolverAlerta(IRepositorioAlertas alertas, IContextoUsuario usuario, IUnidadDeTrabajo unidadDeTrabajo)
{
    public async Task EjecutarAsync(ResolverAlertaComando comando, CancellationToken cancelacion = default)
    {
        var alerta = await alertas.ObtenerAsync(comando.IdAlerta, usuario.IdEstablecimiento, cancelacion)
            ?? throw NoEncontradoError.De("la alerta", comando.IdAlerta);

        alerta.Resolver();
        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
    }
}
