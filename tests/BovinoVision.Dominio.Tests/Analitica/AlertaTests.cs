using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Tests.Analitica;

public class AlertaTests
{
    private static Alerta NuevaAlerta() =>
        new(Guid.CreateVersion7(), TipoAlerta.PerdidaPeso, 1, -1.5m, null, "gmd-v1", DateTimeOffset.UtcNow);

    [Fact]
    public void Una_alerta_activa_se_resuelve()
    {
        var alerta = NuevaAlerta();

        alerta.Resolver();

        Assert.Equal(EstadoAlerta.Resuelta, alerta.Estado);
    }

    [Fact]
    public void Resolver_una_alerta_ya_resuelta_es_un_conflicto()
    {
        var alerta = NuevaAlerta();
        alerta.Resolver();

        var error = Assert.Throws<ConflictoError>(alerta.Resolver);

        Assert.Equal("La alerta ya está resuelta", error.Message);
        Assert.Equal(EstadoAlerta.Resuelta, alerta.Estado);
    }
}
