using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Trazabilidad;

namespace BovinoVision.Aplicacion.Trazabilidad;

public sealed record NuevoLote(string Codigo, string? Nombre = null, decimal? GmdObjetivo = null);

public sealed record CambiosLote(string? Nombre = null, decimal? GmdObjetivo = null);

public sealed class ListarRazas(IConsultasTrazabilidad consultas)
{
    public Task<IReadOnlyList<RazaRespuesta>> EjecutarAsync(CancellationToken cancelacion) =>
        consultas.ListarRazasAsync(cancelacion);
}

public sealed class ListarLotes(IConsultasTrazabilidad consultas, IContextoUsuario contexto)
{
    public Task<IReadOnlyList<LoteRespuesta>> EjecutarAsync(CancellationToken cancelacion) =>
        consultas.ListarLotesAsync(contexto.IdEstablecimiento, cancelacion);
}

public sealed class CrearLote(
    IRepositorioLotes lotes,
    IConsultasTrazabilidad consultas,
    IUnidadDeTrabajo unidadDeTrabajo,
    IContextoUsuario contexto)
{
    public async Task<LoteRespuesta> EjecutarAsync(NuevoLote datos, CancellationToken cancelacion)
    {
        var lote = new Lote(contexto.IdEstablecimiento, datos.Codigo, datos.Nombre, datos.GmdObjetivo);
        if (await lotes.ExisteCodigoAsync(lote.IdEstablecimiento, lote.Codigo, cancelacion))
        {
            throw new ConflictoError($"Ya existe un lote con código {lote.Codigo}");
        }

        lotes.Agregar(lote);
        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        return (await consultas.ObtenerLoteAsync(lote.Id, lote.IdEstablecimiento, cancelacion))!;
    }
}

public sealed class ModificarLote(
    IRepositorioLotes lotes,
    IConsultasTrazabilidad consultas,
    IUnidadDeTrabajo unidadDeTrabajo,
    IContextoUsuario contexto)
{
    public async Task<LoteRespuesta> EjecutarAsync(Guid idLote, CambiosLote cambios, CancellationToken cancelacion)
    {
        var lote = await lotes.ObtenerAsync(idLote, contexto.IdEstablecimiento, cancelacion)
            ?? throw NoEncontradoError.De("el lote", idLote);

        if (cambios.GmdObjetivo is not null)
        {
            lote.DefinirGmdObjetivo(cambios.GmdObjetivo);
        }

        if (cambios.Nombre is not null)
        {
            lotes.Renombrar(lote, cambios.Nombre);
        }

        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        return (await consultas.ObtenerLoteAsync(lote.Id, lote.IdEstablecimiento, cancelacion))!;
    }
}
