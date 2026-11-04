using BovinoVision.Dominio.Usuarios;

namespace BovinoVision.Dominio.Tests.Usuarios;

public class SesionUsuarioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 21, 12, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Una_sesion_vale_hasta_que_vence_o_se_revoca()
    {
        var sesion = new SesionUsuario(Guid.CreateVersion7(), "huella", Ahora, TimeSpan.FromHours(12));

        Assert.Equal(TimeSpan.Zero, sesion.CreadaEn.Offset);
        Assert.Equal(Ahora.AddHours(12), sesion.ExpiraEn);
        Assert.True(sesion.EstaVigente(Ahora.AddHours(12).AddTicks(-1)));
        Assert.False(sesion.EstaVigente(Ahora.AddHours(12)));

        sesion.Revocar(Ahora.AddHours(1));
        sesion.Revocar(Ahora.AddHours(2));

        Assert.Equal(Ahora.AddHours(1), sesion.RevocadaEn);
        Assert.False(sesion.EstaVigente(Ahora.AddHours(1)));
    }
}
