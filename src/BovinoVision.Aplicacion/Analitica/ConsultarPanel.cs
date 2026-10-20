using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Analitica;

namespace BovinoVision.Aplicacion.Analitica;

public sealed record ConsultaPanel(Guid? IdLote);

public sealed record PuntoEvolucion(DateOnly Fecha, decimal? PesoPromedioKg);

/// <param name="AnimalesConGmd">Animals that contributed a GMD to the average (CU-28 3b).</param>
public sealed record PanelRespuesta(
    int CantidadAnimales,
    decimal? PesoPromedioKg,
    decimal? GmdPromedioKgDia,
    int AnimalesConGmd,
    IReadOnlyList<PuntoEvolucion> EvolucionPesoPromedio);

/// <summary>Herd panel: latest weight and most recent GMD per animal, and the average weight per date.</summary>
public sealed class ConsultarPanel(IConsultasAnalitica consultas, IContextoUsuario usuario, CalculadoraGmd calculadora)
{
    public async Task<PanelRespuesta> EjecutarAsync(ConsultaPanel consulta, CancellationToken cancelacion = default)
    {
        var pesajes = await consultas.ListarPesajesAsync(
            new FiltroPesajes(usuario.IdEstablecimiento, consulta.IdLote), cancelacion);

        var porAnimal = pesajes
            .GroupBy(p => p.IdAnimal)
            .Select(g => g.Select(p => new Medicion(p.PesoKg, p.FechaPesaje)).OrderBy(m => m.FechaPesaje).ToList())
            .ToList();

        var ultimosPesos = porAnimal.Select(m => m[^1].PesoKg).ToList();
        var gmdsRecientes = porAnimal
            .Where(m => m.Count >= 2)
            .Select(m => calculadora.Calcular(m[^2], m[^1]))
            .Where(gmd => gmd.HasValue)
            .Select(gmd => gmd!.Value)
            .ToList();

        var evolucion = pesajes
            .GroupBy(p => calculadora.FechaLocal(p.FechaPesaje))
            .OrderBy(g => g.Key)
            .Select(g => new PuntoEvolucion(g.Key, CalculadoraGmd.Promediar(g.Select(p => p.PesoKg).ToList())))
            .ToList();

        return new PanelRespuesta(
            porAnimal.Count,
            CalculadoraGmd.Promediar(ultimosPesos),
            CalculadoraGmd.Promediar(gmdsRecientes),
            gmdsRecientes.Count,
            evolucion);
    }
}
