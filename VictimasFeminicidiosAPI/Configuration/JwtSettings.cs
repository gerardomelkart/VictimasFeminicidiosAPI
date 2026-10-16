namespace VictimasFeminicidiosAPI.Configuration;

public sealed class JwtSettings
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string SecretKey { get; init; } = "";
    public int MinutesToExpire { get; init; }

    public byte[] GetSigningKey()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience)
            || MinutesToExpire is < 1 or > 30)
            throw new InvalidOperationException("Configure Jwt:Issuer, Audience y MinutesToExpire (1 a 30).");
        byte[] key;
        try { key = Convert.FromBase64String(SecretKey); }
        catch (FormatException)
        {
            throw new InvalidOperationException("Jwt:SecretKey requiere al menos 64 bytes aleatorios en Base64, fuera del repositorio.");
        }
        if (key.Length < 64)
            throw new InvalidOperationException("Jwt:SecretKey requiere al menos 64 bytes aleatorios en Base64, fuera del repositorio.");
        return key;
    }
}
