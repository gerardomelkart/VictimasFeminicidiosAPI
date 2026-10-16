using Microsoft.AspNetCore.Authentication.JwtBearer;
using VictimasFeminicidiosAPI.Data;

namespace VictimasFeminicidiosAPI.Services;

public sealed class CuentaTokenEvents(ICuentaRepository repository) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var origen = principal?.FindFirst("origen")?.Value;
        if (origen is not ("EXTERNO" or "SIIID")
            || !int.TryParse(principal?.FindFirst("sub")?.Value, out var id) || id <= 0)
        {
            context.Fail("Token inválido.");
            return;
        }
        // Cada petición comprueba si cambió el rol, contraseña o habilitación.
        var cuenta = await repository.BuscarPorIdAsync(origen, id, context.HttpContext.RequestAborted);
        if (cuenta is null || !cuenta.PuedeAcceder
            || cuenta.Rol != principal?.FindFirst("role")?.Value
            || TokenService.Huella(cuenta) != principal?.FindFirst("credencial")?.Value)
            context.Fail("Cuenta sin acceso.");
    }
}
