using System.Security.Cryptography;
using System.Text;

namespace ChecadorWeb.Security;

/// <summary>
/// Formato heredado de SS_USUARIOS.TX_PASSWORD, idéntico a
/// <c>CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @Password NVARCHAR), 2)</c> usado por
/// sp_desktop_login_admin y sp_seguridad_usuarios: SHA-256 de UTF-16LE, hexadecimal en mayúsculas, sin sal.
/// No cambiar este formato sin un plan de migración aprobado.
/// </summary>
public static class LegacyPasswordHasher
{
    /// <summary>sp_seguridad_usuarios lee la contraseña como nvarchar(50).</summary>
    public const int LongitudMaxima = 50;

    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var bytes = SHA256.HashData(Encoding.Unicode.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    public static bool Verify(string password, string? hashAlmacenado)
    {
        var calculado = Encoding.ASCII.GetBytes(Hash(password));
        if (string.IsNullOrWhiteSpace(hashAlmacenado)) return false;
        var almacenado = Encoding.ASCII.GetBytes(hashAlmacenado.Trim().ToUpperInvariant());
        return CryptographicOperations.FixedTimeEquals(calculado, almacenado);
    }

    /// <summary>Huella corta del hash almacenado para invalidar sesiones al cambiar la contraseña.</summary>
    public static string Sello(string? hashAlmacenado)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("checadorweb|" + (hashAlmacenado ?? string.Empty).ToUpperInvariant()));
        return Convert.ToHexString(bytes, 0, 8);
    }
}
