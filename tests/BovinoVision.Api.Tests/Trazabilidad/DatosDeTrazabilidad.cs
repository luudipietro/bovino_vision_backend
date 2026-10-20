using System.Net;
using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Aplicacion.Trazabilidad;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Pesajes;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Api.Tests.Trazabilidad;

/// <summary>Builds traceability data through the API and weighings directly in the database.</summary>
internal static class DatosDeTrazabilidad
{
    public static async Task<LoteRespuesta> CrearLoteAsync(this HttpClient cliente, string codigo, decimal? gmdObjetivo = null)
    {
        var respuesta = await cliente.EnviarJsonAsync("/lotes", new { codigo, gmdObjetivo });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.LeerAsync<LoteRespuesta>())!;
    }

    public static async Task<AnimalRespuesta> CrearAnimalAsync(this HttpClient cliente, string identificador, Guid? idLote = null)
    {
        var respuesta = await cliente.EnviarJsonAsync("/animales", new { identificador, idLote });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.LeerAsync<AnimalRespuesta>())!;
    }

    public static async Task<T> LeerOkAsync<T>(this HttpClient cliente, string ruta)
    {
        var respuesta = await cliente.GetAsync(new Uri(ruta, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.LeerAsync<T>())!;
    }

    /// <summary>Stores a processed capture and its weighing event, optionally with an alert.</summary>
    public static async Task<EventoPesaje> RegistrarPesajeAsync(
        this FabricaApi fabrica,
        Guid idAnimal,
        decimal pesoKg,
        DateTimeOffset fechaCaptura,
        EstadoAlerta? estadoAlerta = null)
    {
        await using var alcance = fabrica.Services.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();

        var sesion = new SesionCaptura(DatosSemilla.IdUsuarioSistema, fechaCaptura);
        var captura = new Captura(idAnimal, DatosSemilla.IdUsuarioSistema, sesion.Id, fechaCaptura);
        var imagen = captura.AgregarImagen($"{captura.Id}.jpg", Guid.NewGuid().ToString("N"), "image/jpeg");
        captura.SolicitarEstimacion();
        captura.IniciarProcesamiento();
        captura.MarcarProcesada();
        var evento = EventoPesaje.Registrar(captura, imagen.Id, null, pesoKg, fechaCaptura, "mock-v1");
        contexto.AddRange(sesion, captura, evento);

        Alerta? alerta = null;
        if (estadoAlerta is not null)
        {
            alerta = new Alerta(evento.Id, TipoAlerta.PerdidaPeso, 1, -0.5m, null, "test-v1", fechaCaptura);
            contexto.Alertas.Add(alerta);
        }

        await contexto.SaveChangesAsync();

        if (alerta is not null && estadoAlerta == EstadoAlerta.Resuelta)
        {
            await contexto.Alertas
                .Where(a => a.Id == alerta.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Estado, EstadoAlerta.Resuelta));
        }

        return evento;
    }
}
