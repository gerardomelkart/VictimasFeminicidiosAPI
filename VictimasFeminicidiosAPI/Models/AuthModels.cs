using System.ComponentModel.DataAnnotations;

namespace VictimasFeminicidiosAPI.Models;

public sealed class LoginRequest
{
    [Required, StringLength(100)]
    public string Usuario { get; init; } = "";
    [Required, StringLength(200)]
    public string Password { get; init; } = "";
    [Required, RegularExpression("^(EXTERNO|SIIID)$")]
    public string Origen { get; init; } = "EXTERNO";
}

public sealed class CrearUsuarioRequest
{
    [Required, RegularExpression("^[A-Za-z0-9_.@-]{3,100}$")]
    public string Usuario { get; init; } = "";
    [Required, StringLength(200)]
    public string Nombre { get; init; } = "";
    [Required, StringLength(200)]
    public string Institucion { get; init; } = "";
    [Required, StringLength(72, MinimumLength = 12)]
    public string Password { get; init; } = "";
}

public sealed record HabilitarUsuarioRequest(bool Habilitado);
public sealed class CambiarPasswordRequest
{
    [Required, StringLength(72, MinimumLength = 12)]
    public string Password { get; init; } = "";
}

// Uso interno: nunca devolver ni registrar PasswordHash.
public sealed class Cuenta
{
    public int Id { get; init; }
    public string Usuario { get; init; } = "";
    public string PasswordHash { get; init; } = "";
    public string Rol { get; init; } = "";
    public string Version { get; init; } = "";
    public bool RequiereCambioPassword { get; init; }
    public string Origen { get; init; } = "";
    public bool PuedeAcceder => !RequiereCambioPassword
        && ((Origen == "SIIID" && Rol == "SUPER_USUARIO")
            || (Origen == "EXTERNO" && Rol == "CONSULTA_FEMINICIDIOS"));
}
public sealed record UsuarioExterno(int Id, string Usuario, string Nombre, string Institucion, bool Habilitado);
public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresIn);
