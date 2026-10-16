using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VictimasFeminicidiosAPI.Configuration;
using VictimasFeminicidiosAPI.Models;

namespace VictimasFeminicidiosAPI.Services;

public sealed class TokenService(JwtSettings settings)
{
    public LoginResponse Crear(Cuenta cuenta)
    {
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, cuenta.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim("origen", cuenta.Origen),
            new Claim("role", cuenta.Rol),
            new Claim("credencial", Huella(cuenta))
        };
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            notBefore: now, expires: now.AddMinutes(settings.MinutesToExpire),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(settings.GetSigningKey()), SecurityAlgorithms.HmacSha512));
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", settings.MinutesToExpire * 60);
    }
    public static string Huella(Cuenta cuenta) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(cuenta.PasswordHash.Trim() + "|" + cuenta.Version)));
}
