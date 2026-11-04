using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Pesajes;
using BovinoVision.Dominio.Trazabilidad;
using BovinoVision.Infraestructura.Persistencia;

namespace BovinoVision.Api.Tests.Analitica;

/// <summary>Prepara lotes, animales y eventos de pesaje mediante la API del dominio; el test decide cuándo guardar.</summary>
internal sealed class DatosAnalitica(BovinoVisionDbContext contexto, Guid? idUsuario = null)
{
    public static readonly DateTimeOffset Inicio = new(2026, 1, 1, 13, 0, 0, TimeSpan.Zero);

    private SesionCaptura? _sesion;

    public Lote CrearLote(string codigo, decimal? gmdObjetivo = null)
    {
        var lote = new Lote(DatosSemilla.IdEstablecimiento, codigo, gmdObjetivo: gmdObjetivo);
        contexto.Lotes.Add(lote);
        return lote;
    }

    public Animal CrearAnimal(string identificador, Guid? idEstablecimiento = null)
    {
        var animal = new Animal(idEstablecimiento ?? DatosSemilla.IdEstablecimiento, identificador);
        contexto.Animales.Add(animal);
        return animal;
    }

    public static void AsignarLote(Animal animal, Lote lote, DateTimeOffset fecha) =>
        animal.AsignarLote(lote.Id, DatosSemilla.IdUsuarioSistema, fecha);

    /// <summary>Una estimación exitosa capturada en <paramref name="fecha"/>, con el lote vigente en esa fecha.</summary>
    public EventoPesaje Pesar(Animal animal, decimal pesoKg, DateTimeOffset fecha, bool agregar = true)
    {
        if (_sesion is null)
        {
            _sesion = new SesionCaptura(idUsuario ?? DatosSemilla.IdUsuarioSistema, Inicio.AddDays(-1));
            contexto.SesionesCaptura.Add(_sesion);
        }

        var captura = new Captura(animal.Id, idUsuario ?? DatosSemilla.IdUsuarioSistema, _sesion.Id, fecha);
        var imagen = captura.AgregarImagen($"capturas/{captura.Id}.jpg", Guid.NewGuid().ToString("N"), "image/jpeg");
        captura.SolicitarEstimacion();
        captura.IniciarProcesamiento();
        captura.MarcarProcesada();
        contexto.Capturas.Add(captura);

        var evento = EventoPesaje.Registrar(
            captura, imagen.Id, animal.AsignacionEn(fecha)?.Id, pesoKg, fecha.AddMinutes(5), "fake-v1");
        if (agregar)
        {
            contexto.EventosPesaje.Add(evento);
        }

        return evento;
    }

    public Alerta Alertar(EventoPesaje evento, TipoAlerta tipo, int prioridad, DateTimeOffset fechaGeneracion)
    {
        var alerta = new Alerta(evento.Id, tipo, prioridad, -1.5m, tipo == TipoAlerta.Estancamiento ? 1.2m : null, "gmd-v1", fechaGeneracion);
        contexto.Alertas.Add(alerta);
        return alerta;
    }

    public Task GuardarAsync() => contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
}
