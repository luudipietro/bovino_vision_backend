using BovinoVision.Aplicacion.Usuarios;
using Microsoft.AspNetCore.Identity;

namespace BovinoVision.Infraestructura.Usuarios;

/// <summary>Adapter over ASP.NET Core Identity's PBKDF2 hasher (shared framework, no extra package).</summary>
internal sealed class HasherContrasenas : IHasherContrasenas
{
    // Identity's hasher does not use the user instance; one placeholder serves every call.
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
            // Not a hash (e.g. the seeded system user): nobody can log in with it.
            return ResultadoContrasena.Incorrecta;
        }
    }

    public void VerificarFicticio(string contrasena) => Hasher.VerifyHashedPassword(SinUsuario, ResumenFicticio.Value, contrasena);
}
