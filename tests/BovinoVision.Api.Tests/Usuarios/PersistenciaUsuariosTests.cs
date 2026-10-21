using BovinoVision.Api.Tests.Infraestructura;
using BovinoVision.Dominio.Usuarios;
using BovinoVision.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BovinoVision.Api.Tests.Usuarios;

public class PersistenciaUsuariosTests(FabricaApi fabrica) : PruebaDeApi(fabrica)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task La_semilla_crea_los_tres_roles_de_la_ERS_con_sus_permisos()
    {
        await using var alcance = Fabrica.Services.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();

        var roles = await contexto.Roles.AsNoTracking()
            .Where(r => DatosSemilla.Roles.Select(s => s.Id).Contains(r.Id))
            .ToDictionaryAsync(r => r.Nombre, Cancelacion);

        Assert.Equal(
            [Permiso.UsuariosGestionar, Permiso.RolesGestionar, Permiso.AuditoriaConsultar],
            roles["Administrador"].Permisos);
        Assert.True(roles["Administrador"].EsAdministrador);
        Assert.Equal(
            [Permiso.RodeoGestionar, Permiso.RodeoConsultar, Permiso.AnaliticaConsultar],
            roles["Productor Ganadero"].Permisos);
        Assert.Equal(
            [Permiso.CapturasRegistrar, Permiso.RodeoGestionar, Permiso.RodeoConsultar],
            roles["Operario de Campo"].Permisos);
        var sistema = await contexto.Usuarios.AsNoTracking().SingleAsync(u => u.Id == DatosSemilla.IdUsuarioSistema, Cancelacion);
        Assert.False(sistema.Activo);
    }

    [Fact]
    public async Task Los_permisos_editados_y_las_sesiones_se_guardan_y_se_leen()
    {
        var ahora = DateTimeOffset.UtcNow;
        Guid idRol;
        Guid idSesion;
        await using (var alcance = Fabrica.Services.CreateAsyncScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
            var rol = new Rol(DatosSemilla.IdEstablecimiento, "Veterinario", [Permiso.RodeoConsultar]);
            var usuario = new Usuario(DatosSemilla.IdEstablecimiento, rol.Id, "Vet", "vet@campo.com", "hash");
            var sesion = new SesionUsuario(usuario.Id, new string('a', 64), ahora, TimeSpan.FromHours(12));
            contexto.AddRange(rol, usuario, sesion);
            await contexto.SaveChangesAsync(Cancelacion);
            rol.DefinirPermisos([Permiso.AnaliticaConsultar, Permiso.RodeoConsultar]);
            await contexto.SaveChangesAsync(Cancelacion);
            (idRol, idSesion) = (rol.Id, sesion.Id);
        }

        await using (var alcance = Fabrica.Services.CreateAsyncScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<BovinoVisionDbContext>();
            var rol = await contexto.Roles.AsNoTracking().SingleAsync(r => r.Id == idRol, Cancelacion);
            var sesion = await contexto.SesionesUsuario.AsNoTracking().SingleAsync(s => s.Id == idSesion, Cancelacion);

            Assert.Equal([Permiso.RodeoConsultar, Permiso.AnaliticaConsultar], rol.Permisos);
            Assert.True(sesion.EstaVigente(ahora.AddHours(1)));
            await contexto.Usuarios.Where(u => u.Correo == "vet@campo.com").ExecuteDeleteAsync(Cancelacion);
            await contexto.Roles.Where(r => r.Id == idRol).ExecuteDeleteAsync(Cancelacion);
        }
    }
}
