using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VictimasFeminicidiosAPI.Data;
using VictimasFeminicidiosAPI.Models;
using VictimasFeminicidiosAPI.Services;

namespace VictimasFeminicidiosAPI.Controllers;

[ApiController]
[Authorize(Policy = "AdministrarUsuarios")]
[Route("api/v1/usuarios")]
public sealed class UsuariosController(ICuentaRepository repository, ILogger<UsuariosController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) => Ok(await repository.ListarAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Crear(CrearUsuarioRequest request, CancellationToken ct)
    {
        if (!PasswordService.EsNuevaPasswordValida(request.Password)) return PasswordInvalida();
        var administrador = int.Parse(User.FindFirst("sub")!.Value);
        var id = await repository.CrearAsync(request, PasswordService.CrearHash(request.Password), administrador, ct);
        if (id is null) return Conflict(new { mensaje = "El usuario ya existe." });
        logger.LogInformation("Usuario externo {Id} creado por superusuario {Administrador}.", id, administrador);
        return StatusCode(StatusCodes.Status201Created, new { id, request.Usuario });
    }

    [HttpPatch("{id:int:min(1)}/habilitado")]
    public async Task<IActionResult> Habilitar(int id, HabilitarUsuarioRequest request, CancellationToken ct)
    {
        if (!await repository.HabilitarAsync(id, request.Habilitado, ct)) return NotFound();
        logger.LogInformation("Usuario externo {Id}: habilitado {Habilitado}, administrador {Administrador}.",
            id, request.Habilitado, User.FindFirst("sub")!.Value);
        return NoContent();
    }

    [HttpPut("{id:int:min(1)}/password")]
    public async Task<IActionResult> CambiarPassword(int id, CambiarPasswordRequest request, CancellationToken ct)
    {
        if (!PasswordService.EsNuevaPasswordValida(request.Password)) return PasswordInvalida();
        if (!await repository.CambiarPasswordAsync(id, PasswordService.CrearHash(request.Password), ct)) return NotFound();
        logger.LogInformation("Contraseña restablecida para usuario externo {Id} por administrador {Administrador}.",
            id, User.FindFirst("sub")!.Value);
        return NoContent();
    }

    private BadRequestObjectResult PasswordInvalida() => BadRequest(new
    {
        mensaje = "La contraseña debe tener al menos 12 caracteres y como máximo 72 bytes en UTF-8."
    });
}
