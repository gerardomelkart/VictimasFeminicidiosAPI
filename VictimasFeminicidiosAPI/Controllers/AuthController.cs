using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VictimasFeminicidiosAPI.Data;
using VictimasFeminicidiosAPI.Middleware;
using VictimasFeminicidiosAPI.Models;
using VictimasFeminicidiosAPI.Services;

namespace VictimasFeminicidiosAPI.Controllers;

[ApiController]
[Route("apiFeminicidios/auth")]
public sealed class AuthController(ICuentaRepository repository, PasswordService passwords,
    TokenService tokens, ILogger<AuthController> logger) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("token")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        HttpContext.Items[BitacoraMiddleware.IdentidadKey] =
            new BitacoraIdentidad(request.Origen, null, request.Usuario.Trim());
        var cuenta = await repository.BuscarAsync(request.Origen, request.Usuario.Trim(), ct);
        var valida = passwords.Verificar(request.Password, cuenta?.PasswordHash);
        if (!valida || cuenta is null || !cuenta.PuedeAcceder)
        {
            logger.LogWarning("Autenticación rechazada en API de feminicidios.");
            return Unauthorized(new { mensaje = "Usuario o contraseña incorrectos, o cuenta sin acceso." });
        }
        HttpContext.Items[BitacoraMiddleware.IdentidadKey] =
            new BitacoraIdentidad(cuenta.Origen, cuenta.Id, cuenta.Usuario);
        logger.LogInformation("Autenticación correcta. Origen {Origen}, Id {Id}.", cuenta.Origen, cuenta.Id);
        return Ok(tokens.Crear(cuenta));
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        id = User.FindFirst("sub")?.Value,
        origen = User.FindFirst("origen")?.Value,
        rol = User.FindFirst("role")?.Value
    });
}
