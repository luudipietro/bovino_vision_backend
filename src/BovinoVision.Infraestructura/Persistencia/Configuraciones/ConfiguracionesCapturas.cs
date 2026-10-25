using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Trazabilidad;
using BovinoVision.Dominio.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BovinoVision.Infraestructura.Persistencia.Configuraciones;

internal sealed class SesionCapturaConfiguracion : IEntityTypeConfiguration<SesionCaptura>
{
    public void Configure(EntityTypeBuilder<SesionCaptura> builder)
    {
        builder.ToTable("sesiones_captura");
        builder.HasOne<Usuario>().WithMany().HasForeignKey(s => s.IdUsuario).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CapturaConfiguracion : IEntityTypeConfiguration<Captura>
{
    public void Configure(EntityTypeBuilder<Captura> builder)
    {
        builder.ToTable("capturas");
        builder.Property(c => c.MotivoFallo).HasMaxLength(500);
        builder.HasOne<Animal>().WithMany().HasForeignKey(c => c.IdAnimal).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(c => c.IdUsuario).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SesionCaptura>().WithMany().HasForeignKey(c => c.IdSesion).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Imagenes).WithOne().HasForeignKey(i => i.IdCaptura).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Imagenes).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Soportan que el worker de inferencia tome capturas pendientes y recupere concesiones vencidas.
        builder.HasIndex(c => new { c.Estado, c.ProximoIntento });
        builder.HasIndex(c => new { c.Estado, c.VenceProcesamiento });
        builder.HasIndex(c => c.IdSesion);
    }
}

internal sealed class ImagenConfiguracion : IEntityTypeConfiguration<Imagen>
{
    public void Configure(EntityTypeBuilder<Imagen> builder)
    {
        builder.ToTable("imagenes");
        builder.Property(i => i.ReferenciaImagen).HasMaxLength(500);
        builder.Property(i => i.HuellaVerificacion).HasMaxLength(64);
        builder.Property(i => i.TipoContenido).HasMaxLength(100);
    }
}
