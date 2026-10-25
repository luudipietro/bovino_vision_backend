using BovinoVision.Aplicacion.Trazabilidad;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Trazabilidad;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace BovinoVision.Infraestructura.Trazabilidad;

/// <summary>Read models projected in a single SQL statement each, so lists never issue per-row queries.</summary>
internal sealed class ConsultasTrazabilidad(BovinoVisionDbContext contexto, CalculadoraGmd calculadora) : IConsultasTrazabilidad
{
    public async Task<IReadOnlyList<RazaRespuesta>> ListarRazasAsync(CancellationToken cancelacion) =>
        await contexto.Razas.AsNoTracking()
            .OrderBy(r => r.Nombre)
            .Select(r => new RazaRespuesta(r.Id, r.Nombre))
            .ToListAsync(cancelacion);

    public async Task<IReadOnlyList<LoteRespuesta>> ListarLotesAsync(Guid idEstablecimiento, CancellationToken cancelacion) =>
        await ProyectarLotes(contexto.Lotes
                .Where(l => l.IdEstablecimiento == idEstablecimiento)
                .OrderBy(l => l.Codigo))
            .ToListAsync(cancelacion);

    public Task<LoteRespuesta?> ObtenerLoteAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion) =>
        ProyectarLotes(contexto.Lotes.Where(l => l.Id == id && l.IdEstablecimiento == idEstablecimiento))
            .SingleOrDefaultAsync(cancelacion);

    public async Task<IReadOnlyList<AnimalRespuesta>> ListarAnimalesAsync(
        Guid idEstablecimiento,
        FiltroAnimales filtro,
        CancellationToken cancelacion)
    {
        var animales = contexto.Animales.Where(a => a.IdEstablecimiento == idEstablecimiento);

        if (filtro.IdLote is { } idLote)
        {
            animales = animales.Where(a => contexto.HistorialLotes
                .Any(h => h.IdAnimal == a.Id && h.VigenteHasta == null && h.IdLote == idLote));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            var patron = $"%{EscaparComodines(filtro.Busqueda.Trim())}%";
            animales = animales.Where(a => EF.Functions.ILike(a.Identificador, patron));
        }

        if (filtro.SoloConAlertasActivas)
        {
            animales = animales.Where(a => contexto.Alertas.Any(al =>
                al.Estado == EstadoAlerta.Activa
                && contexto.EventosPesaje.Any(e => e.Id == al.IdEvento && e.IdAnimal == a.Id)));
        }

        var filas = await ProyectarAnimales(animales.OrderBy(a => a.Identificador)).ToListAsync(cancelacion);
        return [.. filas.Select(Mapear)];
    }

    public async Task<AnimalRespuesta?> ObtenerAnimalAsync(Guid id, Guid idEstablecimiento, CancellationToken cancelacion)
    {
        var fila = await ProyectarAnimales(contexto.Animales.Where(a => a.Id == id && a.IdEstablecimiento == idEstablecimiento))
            .SingleOrDefaultAsync(cancelacion);
        return fila is null ? null : Mapear(fila);
    }

    public async Task<IReadOnlyList<AsignacionLoteRespuesta>?> ListarHistorialLotesAsync(
        Guid idAnimal,
        Guid idEstablecimiento,
        CancellationToken cancelacion)
    {
        var existe = await contexto.Animales.AnyAsync(a => a.Id == idAnimal && a.IdEstablecimiento == idEstablecimiento, cancelacion);
        if (!existe)
        {
            return null;
        }

        return await contexto.HistorialLotes.AsNoTracking()
            .Where(h => h.IdAnimal == idAnimal)
            .Join(contexto.Lotes, h => h.IdLote, l => l.Id, (h, l) => new { h, l.Codigo })
            .OrderByDescending(x => x.h.VigenteDesde)
            .Select(x => new AsignacionLoteRespuesta(x.h.IdLote, x.Codigo, x.h.VigenteDesde, x.h.VigenteHasta))
            .ToListAsync(cancelacion);
    }

    // Backslash is PostgreSQL's default LIKE escape character.
    private static string EscaparComodines(string texto) =>
        texto.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

    private IQueryable<LoteRespuesta> ProyectarLotes(IQueryable<Lote> lotes) =>
        lotes.AsNoTracking().Select(l => new LoteRespuesta(
            l.Id,
            l.Codigo,
            l.Nombre,
            l.GmdObjetivo,
            contexto.HistorialLotes.Count(h => h.IdLote == l.Id && h.VigenteHasta == null)));

    private AnimalRespuesta Mapear(FilaAnimal fila) => new(
        fila.Id,
        fila.Identificador,
        fila.IdEstablecimiento,
        fila.IdLote,
        fila.LoteCodigo,
        fila.IdRaza,
        fila.Raza,
        fila.Sexo,
        fila.FechaNacimiento,
        fila.UltimoPesoKg,
        fila.AlertasActivas,
        CalcularGmd(fila),
        fila.IdImagenUltimoPesaje);

    /// <summary>Same rule as the weighing history: calendar days between the last two weighings, null without an interval.</summary>
    private decimal? CalcularGmd(FilaAnimal fila) =>
        fila is { UltimoPesoKg: { } peso, UltimaFechaPesaje: { } fecha, PenultimoPesoKg: { } pesoAnterior, PenultimaFechaPesaje: { } fechaAnterior }
            ? calculadora.Calcular(new Medicion(pesoAnterior, fechaAnterior), new Medicion(peso, fecha))
            : null;

    private IQueryable<FilaAnimal> ProyectarAnimales(IQueryable<Animal> animales) =>
        animales.AsNoTracking().Select(a => new FilaAnimal(
            a.Id,
            a.Identificador,
            a.IdEstablecimiento,
            contexto.HistorialLotes
                .Where(h => h.IdAnimal == a.Id && h.VigenteHasta == null)
                .Select(h => (Guid?)h.IdLote)
                .FirstOrDefault(),
            contexto.HistorialLotes
                .Where(h => h.IdAnimal == a.Id && h.VigenteHasta == null)
                .Join(contexto.Lotes, h => h.IdLote, l => l.Id, (h, l) => l.Codigo)
                .FirstOrDefault(),
            a.IdRaza,
            contexto.Razas.Where(r => r.Id == a.IdRaza).Select(r => r.Nombre).FirstOrDefault(),
            a.Sexo,
            a.FechaNacimiento,
            contexto.EventosPesaje
                .Where(e => e.IdAnimal == a.Id)
                .OrderByDescending(e => e.FechaPesaje).ThenByDescending(e => e.Id)
                .Select(e => (decimal?)e.PesoKg)
                .FirstOrDefault(),
            contexto.Alertas.Count(al =>
                al.Estado == EstadoAlerta.Activa
                && contexto.EventosPesaje.Any(e => e.Id == al.IdEvento && e.IdAnimal == a.Id)),
            contexto.EventosPesaje
                .Where(e => e.IdAnimal == a.Id)
                .OrderByDescending(e => e.FechaPesaje).ThenByDescending(e => e.Id)
                .Select(e => (DateTimeOffset?)e.FechaPesaje)
                .FirstOrDefault(),
            contexto.EventosPesaje
                .Where(e => e.IdAnimal == a.Id)
                .OrderByDescending(e => e.FechaPesaje).ThenByDescending(e => e.Id)
                .Skip(1)
                .Select(e => (decimal?)e.PesoKg)
                .FirstOrDefault(),
            contexto.EventosPesaje
                .Where(e => e.IdAnimal == a.Id)
                .OrderByDescending(e => e.FechaPesaje).ThenByDescending(e => e.Id)
                .Skip(1)
                .Select(e => (DateTimeOffset?)e.FechaPesaje)
                .FirstOrDefault(),
            contexto.EventosPesaje
                .Where(e => e.IdAnimal == a.Id)
                .OrderByDescending(e => e.FechaPesaje).ThenByDescending(e => e.Id)
                .Select(e => (Guid?)e.IdImagenUtilizada)
                .FirstOrDefault()));

    /// <summary>A projected animal row: the response fields plus the last two weighings the GMD is computed from.</summary>
    private sealed record FilaAnimal(
        Guid Id,
        string Identificador,
        Guid IdEstablecimiento,
        Guid? IdLote,
        string? LoteCodigo,
        Guid? IdRaza,
        string? Raza,
        Sexo? Sexo,
        DateOnly? FechaNacimiento,
        decimal? UltimoPesoKg,
        int AlertasActivas,
        DateTimeOffset? UltimaFechaPesaje,
        decimal? PenultimoPesoKg,
        DateTimeOffset? PenultimaFechaPesaje,
        Guid? IdImagenUltimoPesaje);
}
