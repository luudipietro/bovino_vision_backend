using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Trazabilidad;

namespace BovinoVision.Aplicacion.Trazabilidad;

public sealed record NuevoAnimal(
    string Identificador,
    Guid? IdRaza = null,
    Sexo? Sexo = null,
    DateOnly? FechaNacimiento = null,
    Guid? IdLote = null);

/// <summary>Null values keep the current state.</summary>
public sealed record CambiosAnimal(
    Guid? IdRaza = null,
    Sexo? Sexo = null,
    DateOnly? FechaNacimiento = null,
    Guid? IdLote = null);

public sealed class ListarAnimales(IConsultasTrazabilidad consultas, IContextoUsuario contexto)
{
    public Task<IReadOnlyList<AnimalRespuesta>> EjecutarAsync(FiltroAnimales filtro, CancellationToken cancelacion) =>
        consultas.ListarAnimalesAsync(contexto.IdEstablecimiento, filtro, cancelacion);
}

public sealed class ObtenerFichaAnimal(IConsultasTrazabilidad consultas, IContextoUsuario contexto)
{
    public async Task<AnimalRespuesta> EjecutarAsync(Guid idAnimal, CancellationToken cancelacion) =>
        await consultas.ObtenerAnimalAsync(idAnimal, contexto.IdEstablecimiento, cancelacion)
            ?? throw NoEncontradoError.De("el animal", idAnimal);
}

public sealed class ObtenerHistorialLotes(IConsultasTrazabilidad consultas, IContextoUsuario contexto)
{
    public async Task<IReadOnlyList<AsignacionLoteRespuesta>> EjecutarAsync(Guid idAnimal, CancellationToken cancelacion) =>
        await consultas.ListarHistorialLotesAsync(idAnimal, contexto.IdEstablecimiento, cancelacion)
            ?? throw NoEncontradoError.De("el animal", idAnimal);
}

public sealed class RegistrarAnimal(
    IRepositorioAnimales animales,
    IRepositorioLotes lotes,
    ICatalogoRazas razas,
    IConsultasTrazabilidad consultas,
    IUnidadDeTrabajo unidadDeTrabajo,
    IContextoUsuario contexto,
    TimeProvider reloj)
{
    public async Task<AnimalRespuesta> EjecutarAsync(NuevoAnimal datos, CancellationToken cancelacion)
    {
        var animal = new Animal(contexto.IdEstablecimiento, datos.Identificador, datos.IdRaza, datos.Sexo, datos.FechaNacimiento);
        if (await animales.ExisteIdentificadorAsync(animal.IdEstablecimiento, animal.Identificador, cancelacion))
        {
            throw new ConflictoError($"Ya existe un animal con identificador {animal.Identificador}");
        }

        await ValidacionesTrazabilidad.ExigirRazaAsync(razas, datos.IdRaza, cancelacion);
        if (datos.IdLote is { } idLote)
        {
            await ValidacionesTrazabilidad.ExigirLoteAsync(lotes, idLote, animal.IdEstablecimiento, cancelacion);
            animal.AsignarLote(idLote, contexto.IdUsuario, reloj.GetUtcNow());
        }

        animales.Agregar(animal);
        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        return (await consultas.ObtenerAnimalAsync(animal.Id, animal.IdEstablecimiento, cancelacion))!;
    }
}

public sealed class ModificarAnimal(
    IRepositorioAnimales animales,
    IRepositorioLotes lotes,
    ICatalogoRazas razas,
    IConsultasTrazabilidad consultas,
    IUnidadDeTrabajo unidadDeTrabajo,
    IContextoUsuario contexto,
    TimeProvider reloj)
{
    public async Task<AnimalRespuesta> EjecutarAsync(Guid idAnimal, CambiosAnimal cambios, CancellationToken cancelacion)
    {
        var animal = await animales.ObtenerAsync(idAnimal, contexto.IdEstablecimiento, cancelacion)
            ?? throw NoEncontradoError.De("el animal", idAnimal);

        await ValidacionesTrazabilidad.ExigirRazaAsync(razas, cambios.IdRaza, cancelacion);
        if (cambios.IdLote is { } idLote)
        {
            await ValidacionesTrazabilidad.ExigirLoteAsync(lotes, idLote, animal.IdEstablecimiento, cancelacion);
            animal.AsignarLote(idLote, contexto.IdUsuario, reloj.GetUtcNow());
        }

        animal.ModificarDatos(cambios.IdRaza, cambios.Sexo, cambios.FechaNacimiento);
        animales.Actualizar(animal);
        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        return (await consultas.ObtenerAnimalAsync(animal.Id, animal.IdEstablecimiento, cancelacion))!;
    }
}

internal static class ValidacionesTrazabilidad
{
    public static async Task ExigirRazaAsync(ICatalogoRazas razas, Guid? idRaza, CancellationToken cancelacion)
    {
        if (idRaza is { } id && !await razas.ExisteAsync(id, cancelacion))
        {
            throw NoEncontradoError.De("la raza", id);
        }
    }

    public static async Task ExigirLoteAsync(IRepositorioLotes lotes, Guid idLote, Guid idEstablecimiento, CancellationToken cancelacion)
    {
        if (await lotes.ObtenerAsync(idLote, idEstablecimiento, cancelacion) is null)
        {
            throw NoEncontradoError.De("el lote", idLote);
        }
    }
}
