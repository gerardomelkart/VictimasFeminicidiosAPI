using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace VictimasFeminicidiosAPI.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public static readonly object RouteKey = new();

    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = context.TraceIdentifier
        });
        try
        {
            await next(context);
        }
        finally
        {
            // Plantilla de ruta: no guardar URL libre, query, cuerpos, cabeceras ni tokens.
            var ruta = context.Items[RouteKey] as string
                ?? (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(sin ruta)";
            var status = context.Response.StatusCode;
            var level = status >= 500 ? LogLevel.Error : status >= 400 ? LogLevel.Warning : LogLevel.Information;
            logger.Log(level,
                "HTTP {Metodo} {Ruta} -> {Estado} en {DuracionMs:0.00} ms. Origen {Origen}, usuario {IdUsuario}.",
                context.Request.Method, ruta, status, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                context.User.Identity?.IsAuthenticated == true ? context.User.FindFirst("origen")?.Value : null,
                context.User.Identity?.IsAuthenticated == true ? context.User.FindFirst("sub")?.Value : null);
        }
    }
}
