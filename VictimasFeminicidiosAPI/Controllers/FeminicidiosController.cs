using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VictimasFeminicidiosAPI.Models;
using VictimasFeminicidiosAPI.Services;

namespace VictimasFeminicidiosAPI.Controllers;

[ApiController]
[Authorize(Roles = "SUPER_USUARIO,CONSULTA_FEMINICIDIOS")]
[Route("apiFeminicidios/consulta")]
public sealed class FeminicidiosController(FeminicidiosService service, FeminicidiosExcel excel,
    ILogger<FeminicidiosController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Consultar([FromQuery] ConsultaFeminicidiosRequest request, CancellationToken ct)
    {
        var anio = request.Anio!.Value;
        var mes = request.Mes!.Value;
        var registros = await service.ConsultarAsync(anio, mes, ct);
        Response.Headers["X-Total-Registros"] = registros.Count.ToString(CultureInfo.InvariantCulture);
        logger.LogInformation("Consulta mensual {Anio}-{Mes}, formato {Formato}, registros {Total}, origen {Origen}, usuario {IdUsuario}.",
            anio, mes, request.Formato, registros.Count, User.FindFirst("origen")?.Value, User.FindFirst("sub")?.Value);
        if (request.Formato == "json") return Ok(registros);
        return File(excel.Generar(registros, ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"feminicidios_{anio:D4}_{mes:D2}.xlsx");
    }
}
