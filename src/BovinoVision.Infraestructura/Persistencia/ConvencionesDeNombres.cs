using System.Text.Json;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Trazabilidad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BovinoVision.Infraestructura.Persistencia;

/// <summary>Mapea los nombres de C# a la nomenclatura del ER: columnas, claves e índices en snake_case; enums como texto snake_case.</summary>
internal static class ConvencionesDeNombres
{
    public static string ASnakeCase(string nombre) => JsonNamingPolicy.SnakeCaseLower.ConvertName(nombre);

    public static void AplicarSnakeCase(ModelBuilder modelBuilder)
    {
        foreach (var entidad in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var propiedad in entidad.GetProperties())
            {
                propiedad.SetColumnName(ASnakeCase(propiedad.Name));
            }

            foreach (var clave in entidad.GetKeys())
            {
                clave.SetName(ASnakeCase(clave.GetDefaultName()!));
            }

            foreach (var foranea in entidad.GetForeignKeys())
            {
                foranea.SetConstraintName(ASnakeCase(foranea.GetDefaultName()!));
            }

            foreach (var indice in entidad.GetIndexes())
            {
                indice.SetDatabaseName(ASnakeCase(indice.GetDefaultDatabaseName()!));
            }
        }
    }

    public static void GuardarEnumsComoTexto(ModelConfigurationBuilder configuracion)
    {
        GuardarComoTexto<Sexo>(configuracion);
        GuardarComoTexto<EstadoSesionCaptura>(configuracion);
        GuardarComoTexto<EstadoCaptura>(configuracion);
        GuardarComoTexto<TipoAlerta>(configuracion);
        GuardarComoTexto<EstadoAlerta>(configuracion);
    }

    private static void GuardarComoTexto<TEnum>(ModelConfigurationBuilder configuracion)
        where TEnum : struct, Enum
    {
        configuracion.Properties<TEnum>().HaveConversion<EnumComoTexto<TEnum>>().HaveMaxLength(40);
    }

    /// <summary>También se usa para colecciones de enums, a las que no llegan las convenciones de propiedades.</summary>
    internal sealed class EnumComoTexto<TEnum>() : ValueConverter<TEnum, string>(
        valor => ATexto(valor),
        texto => DesdeTexto(texto))
        where TEnum : struct, Enum
    {
        private static readonly Dictionary<string, TEnum> PorTexto =
            Enum.GetValues<TEnum>().ToDictionary(ATexto);

        private static string ATexto(TEnum valor) => ASnakeCase(valor.ToString());

        private static TEnum DesdeTexto(string texto) => PorTexto[texto];
    }
}
