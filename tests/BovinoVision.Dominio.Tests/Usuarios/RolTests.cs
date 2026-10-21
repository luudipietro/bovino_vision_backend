using BovinoVision.Dominio.Usuarios;

namespace BovinoVision.Dominio.Tests.Usuarios;

public class RolTests
{
    [Fact]
    public void Los_permisos_del_rol_se_reemplazan_sin_duplicados()
    {
        var rol = new Rol(Guid.CreateVersion7(), "Operario de Campo", [Permiso.CapturasRegistrar]);

        rol.DefinirPermisos([Permiso.RodeoConsultar, Permiso.CapturasRegistrar, Permiso.RodeoConsultar]);

        Assert.Equal([Permiso.CapturasRegistrar, Permiso.RodeoConsultar], rol.Permisos.Order());
        Assert.True(rol.Tiene(Permiso.RodeoConsultar));
        Assert.False(rol.Tiene(Permiso.UsuariosGestionar));
    }

    [Fact]
    public void Un_rol_es_de_administrador_si_puede_gestionar_roles()
    {
        var rol = new Rol(Guid.CreateVersion7(), "Administrador", [Permiso.UsuariosGestionar]);
        Assert.False(rol.EsAdministrador);

        rol.DefinirPermisos([Permiso.RolesGestionar]);

        Assert.True(rol.EsAdministrador);
    }
}
