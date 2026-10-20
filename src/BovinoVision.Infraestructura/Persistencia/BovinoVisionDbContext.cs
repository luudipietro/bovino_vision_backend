using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Analitica;
using BovinoVision.Dominio.Capturas;
using BovinoVision.Dominio.Establecimientos;
using BovinoVision.Dominio.Pesajes;
using BovinoVision.Dominio.Trazabilidad;
using BovinoVision.Dominio.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace BovinoVision.Infraestructura.Persistencia;

public sealed class BovinoVisionDbContext(DbContextOptions<BovinoVisionDbContext> opciones)
    : DbContext(opciones), IUnidadDeTrabajo
{
    public DbSet<Establecimiento> Establecimientos => Set<Establecimiento>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Raza> Razas => Set<Raza>();
    public DbSet<Lote> Lotes => Set<Lote>();
    public DbSet<Animal> Animales => Set<Animal>();
    public DbSet<HistorialLote> HistorialLotes => Set<HistorialLote>();
    public DbSet<SesionCaptura> SesionesCaptura => Set<SesionCaptura>();
    public DbSet<Captura> Capturas => Set<Captura>();
    public DbSet<Imagen> Imagenes => Set<Imagen>();
    public DbSet<EventoPesaje> EventosPesaje => Set<EventoPesaje>();
    public DbSet<Alerta> Alertas => Set<Alerta>();

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) => SaveChangesAsync(cancelacion);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BovinoVisionDbContext).Assembly);
        ConvencionesDeNombres.AplicarSnakeCase(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ConvencionesDeNombres.GuardarEnumsComoTexto(configurationBuilder);
    }
}
