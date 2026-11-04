using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Pesajes;
using BovinoVision.Dominio.Trazabilidad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BovinoVision.Infraestructura.Persistencia.Configuraciones;

internal sealed class EventoPesajeConfiguracion : IEntityTypeConfiguration<EventoPesaje>
{
    public void Configure(EntityTypeBuilder<EventoPesaje> builder)
    {
        builder.ToTable("eventos_pesaje");
        builder.Property(e => e.PesoKg).HasPrecision(7, 2);
        builder.Property(e => e.VersionModelo).HasMaxLength(100);
        builder.HasOne<Animal>().WithMany().HasForeignKey(e => e.IdAnimal).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Captura>().WithMany().HasForeignKey(e => e.IdCaptura).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Imagen>().WithMany().HasForeignKey(e => e.IdImagenUtilizada).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HistorialLote>().WithMany().HasForeignKey(e => e.IdHistorialLote).OnDelete(DeleteBehavior.Restrict);

        // Como máximo un evento de pesaje por captura y por imagen.
        builder.HasIndex(e => e.IdCaptura).IsUnique();
        builder.HasIndex(e => e.IdImagenUtilizada).IsUnique();
        builder.HasIndex(e => new { e.IdAnimal, e.FechaPesaje });
    }
}

internal sealed class AlertaConfiguracion : IEntityTypeConfiguration<Alerta>
{
    public void Configure(EntityTypeBuilder<Alerta> builder)
    {
        builder.ToTable("alertas");
        builder.Property(a => a.Variacion).HasPrecision(8, 3);
        builder.Property(a => a.GmdEsperada).HasPrecision(6, 3);
        builder.Property(a => a.VersionRegla).HasMaxLength(50);
        builder.HasOne<EventoPesaje>().WithMany().HasForeignKey(a => a.IdEvento).OnDelete(DeleteBehavior.Restrict);

        // Reevaluar un evento nunca duplica su alerta.
        builder.HasIndex(a => a.IdEvento).IsUnique();
        builder.HasIndex(a => new { a.Estado, a.Prioridad });
    }
}
