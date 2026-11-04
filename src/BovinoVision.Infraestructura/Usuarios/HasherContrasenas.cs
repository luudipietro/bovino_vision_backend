using BovinoVision.Aplicacion.Usuarios;
using Microsoft.AspNetCore.Identity;

namespace BovinoVision.Infraestructura.Usuarios;

/// <summary>Adaptador sobre el hasher PBKDF2 de ASP.NET Core Identity (framework compartido, sin paquete extra).</summary>
internal sealed class HasherContrasenas : IHasherContrasenas
{
    // El hasher de Identity no usa la instancia del usuario; un único marcador sirve para todas las llamadas.
    private static readonly object SinUsuario = new();
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly Lazy<string> ResumenFicticio = new(() => Hasher.HashPassword(SinUsuario, Guid.NewGuid().ToString()));

    public string Generar(string contrasena) => Hasher.HashPassword(SinUsuario, contrasena);

    public ResultadoContrasena Verificar(string resumen, string contrasena)
    {
        try
        {
            return Hasher.VerifyHashedPassword(SinUsuario, resumen, contrasena) switch
            {
                PasswordVerificationResult.Success => ResultadoContrasena.Correcta,
                PasswordVerificationResult.SuccessRehashNeeded => ResultadoContrasena.CorrectaDebeActualizarse,
                _ => ResultadoContrasena.Incorrecta,
            };
        }
        catch (FormatException)
        {
            // No es un hash (por ejemplo, el usuario del sistema de la semilla): nadie puede iniciar sesión con él.
            return ResultadoContrasena.Incorrecta;
        }
    }

    public void VerificarFicticio(string contrasena) => Hasher.VerifyHashedPassword(SinUsuario, ResumenFicticio.Value, contrasena);
}
