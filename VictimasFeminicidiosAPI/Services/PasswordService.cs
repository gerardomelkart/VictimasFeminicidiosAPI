using System.Text;

namespace VictimasFeminicidiosAPI.Services;

public sealed class PasswordService
{
    // Costo equivalente para una cuenta inexistente.
    private readonly string _dummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), workFactor: 12);
    public bool Verificar(string password, string? hash)
    {
        if (Encoding.UTF8.GetByteCount(password) > 72) return false;
        try
        {
            var valid = BCrypt.Net.BCrypt.Verify(password, string.IsNullOrWhiteSpace(hash) ? _dummyHash : hash.Trim());
            return hash is not null && valid;
        }
        catch (BCrypt.Net.SaltParseException) { return false; }
    }
    public static bool EsNuevaPasswordValida(string password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length >= 12 && Encoding.UTF8.GetByteCount(password) <= 72;
    public static string CrearHash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
}
