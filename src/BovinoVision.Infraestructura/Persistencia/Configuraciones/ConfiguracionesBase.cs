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

        builder.HasData(new
        {
            Id = DatosSemilla.IdRolAdministrador,
            IdEstablecimiento = DatosSemilla.IdEstablecimiento,
            Nombre = "Administrador",
        });
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
            Activo = true,
            IntentosFallidos = 0,
        });
    }
}
