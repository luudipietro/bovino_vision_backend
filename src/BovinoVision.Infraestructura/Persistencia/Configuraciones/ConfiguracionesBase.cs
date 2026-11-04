using BovinoVision.Dominio.Establecimientos;
using BovinoVision.Dominio.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BovinoVision.Infraestructura.Persistencia.Configuraciones;

internal sealed class EstablecimientoConfiguracion : IEntityTypeConfiguration<Establecimiento>
{
    public void Configure(EntityTypeBuilder<Establecimiento> builder)
    {
        builder.ToTable("establecimientos");
        builder.Property(e => e.Nombre).HasMaxLength(200);
        builder.Property(e => e.Ubicacion).HasMaxLength(300);

        builder.HasData(new { Id = DatosSemilla.IdEstablecimiento, Nombre = DatosSemilla.NombreEstablecimiento });
    }
}

internal sealed class RolConfiguracion : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        builder.ToTable("roles");
        builder.Property(r => r.Nombre).HasMaxLength(100);
        builder.HasOne<Establecimiento>().WithMany().HasForeignKey(r => r.IdEstablecimiento).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(r => r.EsAdministrador);
        // Una columna text[]: los permisos se leen con el rol en cada solicitud autenticada.
        builder.PrimitiveCollection(r => r.Permisos)
            .ElementType(elemento => elemento.HasConversion(new ConvencionesDeNombres.EnumComoTexto<Permiso>()));

        builder.HasData(DatosSemilla.Roles.Select(rol => new
        {
            rol.Id,
            IdEstablecimiento = DatosSemilla.IdEstablecimiento,
            rol.Nombre,
            Permisos = (IReadOnlyList<Permiso>)rol.Permisos,
        }));
    }
}

internal sealed class SesionUsuarioConfiguracion : IEntityTypeConfiguration<SesionUsuario>
{
    public void Configure(EntityTypeBuilder<SesionUsuario> builder)
    {
        builder.ToTable("sesiones_usuario");
        builder.Property(s => s.Huella).HasMaxLength(64);
        builder.HasIndex(s => s.Huella).IsUnique();
        builder.HasOne<Usuario>().WithMany().HasForeignKey(s => s.IdUsuario).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.Property(u => u.Nombre).HasMaxLength(200);
        builder.Property(u => u.Correo).HasMaxLength(254);
        builder.Property(u => u.ResumenContrasena).HasMaxLength(500);
        builder.HasIndex(u => u.Correo).IsUnique();
        builder.HasOne<Establecimiento>().WithMany().HasForeignKey(u => u.IdEstablecimiento).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Rol>().WithMany().HasForeignKey(u => u.IdRol).OnDelete(DeleteBehavior.Restrict);

        builder.HasData(new
        {
            Id = DatosSemilla.IdUsuarioSistema,
            IdEstablecimiento = DatosSemilla.IdEstablecimiento,
            IdRol = DatosSemilla.IdRolAdministrador,
            Nombre = "Sistema",
            Correo = DatosSemilla.CorreoUsuarioSistema,
            ResumenContrasena = DatosSemilla.ContrasenaInutilizable,
            Activo = false,
            IntentosFallidos = 0,
        });
    }
}
