using BovinoVision.Aplicacion.Analitica;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Pesajes;

namespace BovinoVision.Aplicacion.Tests.Analitica;

public class EvaluadorAlertasNuloTests
{
    [Fact]
    public async Task Nunca_genera_alertas()
    {
        var captura = new Captura(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        var imagen = captura.AgregarImagen("ref.jpg", "hash", "image/jpeg");
        var evento = EventoPesaje.Registrar(captura, imagen.Id, null, 300m, DateTimeOffset.UtcNow, "v");

        var alerta = await new EvaluadorAlertasNulo().EvaluarAsync(evento, TestContext.Current.CancellationToken);

        Assert.Null(alerta);
    }
}
