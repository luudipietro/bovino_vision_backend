using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Analitica;

public sealed record ResolverAlertaComando(Guid IdAlerta);

/// <summary>Closes an active alert of the establecimiento; it stops showing up among the active ones.</summary>
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
