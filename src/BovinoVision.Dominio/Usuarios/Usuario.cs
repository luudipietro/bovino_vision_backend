using BovinoVision.Dominio.Comun;

namespace BovinoVision.Dominio.Usuarios;

/// <summary>Usuario del sistema; es dueño de su estado de bloqueo (CU-01) y de su baja lógica (CU-05).</summary>
public sealed class Usuario : Entidad
{
    public const int MaximoIntentosFallidos = 5;
    public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(5);

    public Guid IdEstablecimiento { get; private set; }
    public Guid IdRol { get; private set; }
    public string Nombre { get; private set; } = null!;
    public string Correo { get; private set; } = null!;
    public string ResumenContrasena { get; private set; } = null!;
    public bool Activo { get; private set; }
    public int IntentosFallidos { get; private set; }
    public DateTimeOffset? BloqueadoHasta { get; private set; }

    private Usuario() { }

    public Usuario(Guid idEstablecimiento, Guid idRol, string nombre, string correo, string resumenContrasena)
    {
        IdEstablecimiento = idEstablecimiento;
        IdRol = idRol;
        ModificarDatos(nombre, correo);
        ResumenContrasena = resumenContrasena;
        Activo = true;
    }

    /// <summary>Los correos se comparan sin distinguir mayúsculas, así que se guardan en minúsculas.</summary>
    public static string NormalizarCorreo(string? correo) => (correo ?? string.Empty).Trim().ToLowerInvariant();

    public void ModificarDatos(string nombre, string correo)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new OperacionInvalidaError("El nombre del usuario es obligatorio");
        }

        var normalizado = NormalizarCorreo(correo);
        var arroba = normalizado.IndexOf('@', StringComparison.Ordinal);
        if (arroba <= 0 || arroba == normalizado.Length - 1 || normalizado.Contains(' ', StringComparison.Ordinal))
        {
            throw new OperacionInvalidaError("El correo electrónico no es válido");
        }

        Nombre = nombre.Trim();
        Correo = normalizado;
    }

    public void CambiarContrasena(string resumenContrasena) => ResumenContrasena = resumenContrasena;

    public void AsignarRol(Guid idRol)
    {
        if (idRol == IdRol)
        {
            throw new OperacionInvalidaError("El usuario ya tiene ese rol");
        }

        IdRol = idRol;
    }

    public void DarDeBaja()
    {
        if (!Activo)
        {
            throw new OperacionInvalidaError("El usuario ya está dado de baja");
        }

        Activo = false;
    }

    public bool EstaBloqueado(DateTimeOffset ahora) => BloqueadoHasta > ahora;

    public TimeSpan TiempoRestanteDeBloqueo(DateTimeOffset ahora) =>
        EstaBloqueado(ahora) ? BloqueadoHasta!.Value - ahora : TimeSpan.Zero;

    /// <summary>La quinta falla consecutiva bloquea la cuenta; un bloqueo vencido inicia un conteo nuevo.</summary>
    public void RegistrarIntentoFallido(DateTimeOffset ahora)
    {
        if (EstaBloqueado(ahora))
        {
            return;
        }

        if (BloqueadoHasta is not null)
        {
            BloqueadoHasta = null;
            IntentosFallidos = 0;
        }

        IntentosFallidos++;
        if (IntentosFallidos >= MaximoIntentosFallidos)
        {
            BloqueadoHasta = ahora.ToUniversalTime() + DuracionBloqueo;
        }
    }

    public void RegistrarIngresoExitoso()
    {
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }
}
