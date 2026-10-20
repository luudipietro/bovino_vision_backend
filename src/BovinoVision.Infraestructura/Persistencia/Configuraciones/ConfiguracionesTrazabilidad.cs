using BovinoVision.Dominio.Establecimientos;
using BovinoVision.Dominio.Trazabilidad;
using BovinoVision.Dominio.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BovinoVision.Infraestructura.Persistencia.Configuraciones;

internal sealed class RazaConfiguracion : IEntityTypeConfiguration<Raza>
{
    public void Configure(EntityTypeBuilder<Raza> builder)
    {
        builder.ToTable("razas");
        builder.Property(r => r.Nombre).HasMaxLength(100);
        builder.HasIndex(r => r.Nombre).IsUnique();

        builder.HasData(DatosSemilla.Razas.Select(r => new { r.Id, r.Nombre }));
    }
}

internal sealed class LoteConfiguracion : IEntityTypeConfiguration<Lote>
{
    public void Configure(EntityTypeBuilder<Lote> builder)
    {
        builder.ToTable("lotes");
        builder.Property(l => l.Codigo).HasMaxLength(50);
        builder.Property(l => l.Nombre).HasMaxLength(200);
        builder.Property(l => l.GmdObjetivo).HasPrecision(6, 3);
        builder.HasIndex(l => new { l.IdEstablecimiento, l.Codigo }).IsUnique();
        builder.HasOne<Establecimiento>().WithMany().HasForeignKey(l => l.IdEstablecimiento).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AnimalConfiguracion : IEntityTypeConfiguration<Animal>
{
    public void Configure(EntityTypeBuilder<Animal> builder)
    {
        builder.ToTable("animales");
        builder.Property(a => a.Identificador).HasMaxLength(50);
        builder.HasIndex(a => new { a.IdEstablecimiento, a.Identificador }).IsUnique();
        builder.HasOne<Establecimiento>().WithMany().HasForeignKey(a => a.IdEstablecimiento).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Raza>().WithMany().HasForeignKey(a => a.IdRaza).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.HistorialLotes).WithOne().HasForeignKey(h => h.IdAnimal).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(a => a.HistorialLotes).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(a => a.AsignacionVigente);
    }
}

internal sealed class HistorialLoteConfiguracion : IEntityTypeConfiguration<HistorialLote>
{
    public void Configure(EntityTypeBuilder<HistorialLote> builder)
    {
        builder.ToTable("historial_lotes");
        builder.Ignore(h => h.EstaVigente);
        builder.HasOne<Lote>().WithMany().HasForeignKey(h => h.IdLote).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(h => h.IdUsuario).OnDelete(DeleteBehavior.Restrict);

        // An animal has at most one open assignment.
        builder.HasIndex(h => h.IdAnimal).IsUnique().HasFilter("vigente_hasta IS NULL");
        builder.HasIndex(h => new { h.IdLote, h.VigenteHasta });
    }
}
