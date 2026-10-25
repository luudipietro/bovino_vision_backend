using System.Security.Cryptography;
using BovinoVision.Aplicacion.Comun;
using BovinoVision.Dominio.Comun;
using BovinoVision.Dominio.Usuarios;

namespace BovinoVision.Aplicacion.Usuarios;

/// <summary>Tokens de sesión opacos de 256 bits; solo se guarda su hash SHA-256.</summary>
public static class TokensDeSesion
{
    public static (string Token, string Huella) Generar()
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (token, Huella(token));
    }

    public static string Huella(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public static class Contrasenas
{
    public const int LargoMinimo = 8;

    public static void Validar(string contrasena)
    {
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < LargoMinimo)
        {
            throw new OperacionInvalidaError($"La contraseña debe tener al menos {LargoMinimo} caracteres");
        }
    }
}

public sealed record CredencialesUsuario(string Correo, string Contrasena);

/// <summary>CU-01: login con bloqueo después de cinco fallas consecutivas.</summary>
public sealed class IniciarSesion(
    IRepositorioUsuarios usuarios,
    IRepositorioRoles roles,
    IRepositorioSesionesUsuario sesiones,
    IHasherContrasenas hasher,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider tiempo,
    OpcionesSesion opciones)
{
    private const string CredencialesIncorrectas = "Usuario o contraseña incorrectos.";

    public async Task<SesionIniciada> EjecutarAsync(CredencialesUsuario credenciales, CancellationToken cancelacion = default)
    {
        // Una solicitud sin campos es solo un caso de credenciales incorrectas, nunca un error del servidor.
        credenciales = new CredencialesUsuario(credenciales.Correo ?? string.Empty, credenciales.Contrasena ?? string.Empty);
        var usuario = await usuarios.ObtenerPorCorreoAsync(Usuario.NormalizarCorreo(credenciales.Correo), cancelacion);
        if (usuario is null)
        {
            hasher.VerificarFicticio(credenciales.Contrasena);
            throw new NoAutenticadoError(CredencialesIncorrectas);
        }

        var ahora = tiempo.GetUtcNow();
        if (usuario.EstaBloqueado(ahora))
        {
            throw new UsuarioBloqueadoError(usuario.TiempoRestanteDeBloqueo(ahora));
        }

        var resultado = hasher.Verificar(usuario.ResumenContrasena, credenciales.Contrasena);
        if (resultado == ResultadoContrasena.Incorrecta)
        {
            usuario.RegistrarIntentoFallido(ahora);
            await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
            if (usuario.EstaBloqueado(ahora))
            {
                throw new UsuarioBloqueadoError(usuario.TiempoRestanteDeBloqueo(ahora));
            }

            throw new NoAutenticadoError(CredencialesIncorrectas);
        }

        // Solo quien conoce la contraseña se entera de que la cuenta fue dada de baja.
        if (!usuario.Activo)
        {
            throw new AccesoDenegadoError("No es posible acceder con esta cuenta. Contactá a un administrador.");
        }

        var rol = await roles.ObtenerAsync(usuario.IdRol, usuario.IdEstablecimiento, cancelacion)
            ?? throw NoEncontradoError.De("el rol", usuario.IdRol);
        usuario.RegistrarIngresoExitoso();
        if (resultado == ResultadoContrasena.CorrectaDebeActualizarse)
        {
            usuario.CambiarContrasena(hasher.Generar(credenciales.Contrasena));
        }

        var (token, huella) = TokensDeSesion.Generar();
        var sesion = new SesionUsuario(usuario.Id, huella, ahora, opciones.Duracion);
        sesiones.Agregar(sesion);
        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        return new SesionIniciada(token, new SesionRespuesta(UsuarioRespuesta.Desde(usuario, rol, sesion.CreadaEn), rol.Permisos, sesion.ExpiraEn));
    }
}

/// <summary>CU-02: el token deja de funcionar de inmediato; un token desconocido se ignora.</summary>
public sealed class CerrarSesion(IRepositorioSesionesUsuario sesiones, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider tiempo)
{
    public async Task EjecutarAsync(string token, CancellationToken cancelacion = default)
    {
        var sesion = await sesiones.ObtenerPorHuellaAsync(TokensDeSesion.Huella(token), cancelacion);
        if (sesion is null)
        {
            return;
        }

        sesion.Revocar(tiempo.GetUtcNow());
        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
    }
}

/// <summary>Valida un token en cada solicitud autenticada.</summary>
public sealed class ValidarSesion(IConsultasSesion consultas, TimeProvider tiempo)
{
    public async Task<SesionActiva?> EjecutarAsync(string token, CancellationToken cancelacion = default)
    {
        var sesion = await consultas.ObtenerAsync(TokensDeSesion.Huella(token), cancelacion);
        return sesion is not null && sesion.EsValidaEn(tiempo.GetUtcNow()) ? sesion : null;
    }
}

public sealed record AdministradorInicial(Guid IdEstablecimiento, Guid IdRol, string Nombre, string Correo, string Contrasena);

/// <summary>Crea el primer administrador desde la configuración cuando ningún usuario tiene todavía ese correo.</summary>
public sealed class CrearAdministradorInicial(
    IRepositorioUsuarios usuarios,
    IRepositorioRoles roles,
    IHasherContrasenas hasher,
    IUnidadDeTrabajo unidadDeTrabajo)
{
    public async Task<bool> EjecutarAsync(AdministradorInicial datos, CancellationToken cancelacion = default)
    {
        if (await usuarios.ObtenerPorCorreoAsync(Usuario.NormalizarCorreo(datos.Correo), cancelacion) is not null)
        {
            return false;
        }

        Contrasenas.Validar(datos.Contrasena);
        var rol = await roles.ObtenerAsync(datos.IdRol, datos.IdEstablecimiento, cancelacion)
            ?? throw NoEncontradoError.De("el rol", datos.IdRol);
        usuarios.Agregar(new Usuario(datos.IdEstablecimiento, rol.Id, datos.Nombre, datos.Correo, hasher.Generar(datos.Contrasena)));
        await unidadDeTrabajo.GuardarCambiosAsync(cancelacion);
        return true;
    }
}
