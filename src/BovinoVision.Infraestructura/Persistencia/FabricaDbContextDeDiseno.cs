using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BovinoVision.Infraestructura.Persistencia;

/// <summary>Permite que `dotnet ef` construya el modelo sin iniciar la API.</summary>
internal sealed class FabricaDbContextDeDiseno : IDesignTimeDbContextFactory<BovinoVisionDbContext>
{
    private const string ConexionLocal =
        "Host=localhost;Port=5433;Database=bovinovision;Username=bovinovision;Password=bovinovision";

    public BovinoVisionDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ConnectionStrings__BovinoVision") ?? ConexionLocal;
        var opciones = new DbContextOptionsBuilder<BovinoVisionDbContext>().UseNpgsql(conexion).Options;
        return new BovinoVisionDbContext(opciones);
    }
}
