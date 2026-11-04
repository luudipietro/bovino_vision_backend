using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Comun;

namespace BovinoVision.Aplicacion.Analitica;

/// <param name="Desde">Primera fecha calendario local del período (inclusive).</param>
/// <param name="Hasta">Última fecha calendario local del período (inclusive).</param>
public sealed record ConsultaReporte(DateOnly Desde, DateOnly Hasta, Guid? IdLote);

public sealed record ReporteRespuesta(
    DateOnly Desde,
    DateOnly Hasta,
    int CantidadPesajes,
    int CantidadAnimales,
    decimal? PesoPromedioKg,
    decimal? GmdPromedioKgDia);

/// <summary>Reporte de pesajes de un período: cantidades, peso promedio y la GMD entre pesajes consecutivos dentro de él.</summary>
public sealed class GenerarReporte(IConsultasAnalitica consultas, IContextoUsuario usuario, CalculadoraGmd calculadora)
{
    public async Task<ReporteRespuesta> EjecutarAsync(ConsultaReporte consulta, CancellationToken cancelacion = default)
    {
        if (consulta.Desde > consulta.Hasta)
        {
            throw new OperacionInvalidaError("La fecha 'desde' no puede ser posterior a 'hasta'");
        }

        var filtro = new FiltroPesajes(
            usuario.IdEstablecimiento,
            consulta.IdLote,
            calculadora.InicioDelDia(consulta.Desde),
            calculadora.InicioDelDia(consulta.Hasta.AddDays(1)));
        var pesajes = await consultas.ListarPesajesAsync(filtro, cancelacion);

        var porAnimal = pesajes.GroupBy(p => p.IdAnimal).ToList();
        var gmds = porAnimal
            .SelectMany(g => calculadora.GmdEntreConsecutivos(g.Select(p => new Medicion(p.PesoKg, p.FechaPesaje))))
            .ToList();

        return new ReporteRespuesta(
            consulta.Desde,
            consulta.Hasta,
            pesajes.Count,
            porAnimal.Count,
            CalculadoraGmd.Promediar(pesajes.Select(p => p.PesoKg).ToList()),
            CalculadoraGmd.Promediar(gmds));
    }
}
