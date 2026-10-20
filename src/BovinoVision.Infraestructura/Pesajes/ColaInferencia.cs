using BovinoVision.Aplicacion.Comun;
using BovinoVision.Aplicacion.Pesajes;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Pesajes;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace BovinoVision.Infraestructura.Pesajes;

internal sealed class ColaInferencia(BovinoVisionDbContext contexto, IUnidadDeTrabajo unidadDeTrabajo) : IColaInferencia
{
    public async Task<TrabajoInferencia?> ReclamarAsync(DateTimeOffset ahora, TimeSpan arrendamiento, int maximoIntentos, CancellationToken cancelacion)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        var candidatos = await contexto.Capturas.FromSqlInterpolated($"""
            SELECT * FROM capturas
            WHERE (estado = 'pendiente' AND (proximo_intento IS NULL OR proximo_intento <= {ahora}))
               OR (estado = 'procesando' AND proximo_intento <= {ahora})
            ORDER BY fecha_captura, id LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancelacion);
        var captura = candidatos.SingleOrDefault();
        if (captura is null) { return null; }
        await contexto.Entry(captura).Collection(c => c.Imagenes).LoadAsync(cancelacion);

        if (captura.Estado == EstadoCaptura.Procesando)
        {
            if (captura.Intentos >= maximoIntentos)
            {
                captura.MarcarFallida("Se agotaron los intentos de estimación tras vencer el procesamiento");
                await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
                await transaccion.CommitAsync(cancelacion);
                contexto.ChangeTracker.Clear();
                return null;
            }
            captura.ReintentarDespues("Venció el procesamiento anterior", ahora);
        }
        captura.IniciarProcesamiento(ahora.Add(arrendamiento));
        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);
        var trabajo = new TrabajoInferencia(captura.Id, captura.Intentos, captura.Imagenes.OrderBy(i => i.Id).First().ReferenciaImagen);
        // Completion must re-read the row after inference; the earlier snapshot cannot fence late workers.
        contexto.ChangeTracker.Clear();
        return trabajo;
    }

    public async Task ConCapturaBloqueadaAsync(Guid id, Func<Captura, Task> accion, CancellationToken cancelacion)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        var filas = await contexto.Capturas.FromSqlInterpolated($"SELECT * FROM capturas WHERE id = {id} FOR UPDATE").ToListAsync(cancelacion);
        var captura = filas.SingleOrDefault() ?? throw NoEncontradoError.De("la captura", id);
        await contexto.Entry(captura).Collection(c => c.Imagenes).LoadAsync(cancelacion);
        await accion(captura);
        await transaccion.CommitAsync(cancelacion);
    }

    public async Task<Guid?> ResolverHistorialAsync(Guid idAnimal, DateTimeOffset fecha, CancellationToken cancelacion)
    {
        var animal = await contexto.Animales.Include(a => a.HistorialLotes).SingleAsync(a => a.Id == idAnimal, cancelacion);
        return animal.AsignacionEn(fecha)?.Id;
    }

    public void Agregar(EventoPesaje evento) => contexto.EventosPesaje.Add(evento);
}
