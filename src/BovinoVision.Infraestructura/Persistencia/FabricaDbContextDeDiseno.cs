using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BovinoVision.Infraestructura.Persistencia;

/// <summary>Lets `dotnet ef` build the model without starting the API.</summary>
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
