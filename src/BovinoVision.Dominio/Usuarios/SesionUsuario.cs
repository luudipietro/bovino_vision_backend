using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Usuarios;

/// <summary>Login session; only the hash of its token is stored, so a database leak does not expose tokens.</summary>
public sealed class SesionUsuario : Entidad
{
    public Guid IdUsuario { get; private set; }
    public string Huella { get; private set; } = null!;
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset ExpiraEn { get; private set; }
    public DateTimeOffset? RevocadaEn { get; private set; }

    private SesionUsuario() { }

    public SesionUsuario(Guid idUsuario, string huella, DateTimeOffset ahora, TimeSpan duracion)
    {
        IdUsuario = idUsuario;
        Huella = huella;
        CreadaEn = ahora.ToUniversalTime();
        ExpiraEn = CreadaEn + duracion;
    }

    public bool EstaVigente(DateTimeOffset ahora) => RevocadaEn is null && ahora < ExpiraEn;

    public void Revocar(DateTimeOffset ahora) => RevocadaEn ??= ahora.ToUniversalTime();
}
