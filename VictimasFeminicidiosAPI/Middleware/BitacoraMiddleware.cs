using System.Diagnostics;
using System.Globalization;
using VictimasFeminicidiosAPI.Data;
using VictimasFeminicidiosAPI.Models;
using VictimasFeminicidiosAPI.Services;

namespace VictimasFeminicidiosAPI.Middleware;

public sealed class BitacoraMiddleware(RequestDelegate next, ILogger<BitacoraMiddleware> logger)
{
    public static readonly object IdentidadKey = new();

    public async Task InvokeAsync(HttpContext context, IBitacoraRepository repository)
    {
        var login = HttpMethods.IsPost(context.Request.Method)
            && string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/apiFeminicidios/auth/token", StringComparison.OrdinalIgnoreCase);
        var consulta = HttpMethods.IsGet(context.Request.Method)
            && string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/apiFeminicidios/consulta", StringComparison.OrdinalIgnoreCase);
        if (!login && !consulta) { await next(context); return; }

        var inicio = HoraCiudadMexico.ConvertirDesdeUtc(DateTime.UtcNow);
        var reloj = Stopwatch.GetTimestamp();
        var salida = context.Response.Body;
        // No entregar un token ni datos si falla el registro obligatorio en SQL.
        // El buffer también incluye la serialización JSON y la generación de Excel.
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            try { await next(context); }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                context.Response.StatusCode = 499;
            }
            var identidad = context.Items[IdentidadKey] as BitacoraIdentidad;
            var autenticado = context.User.Identity?.IsAuthenticated == true;
            var estado = context.Response.StatusCode;
            var evento = new BitacoraEvento(inicio, login ? "LOGIN" : "CONSULTA",
                identidad?.Origen ?? (autenticado ? context.User.FindFirst("origen")?.Value : null),
                identidad?.IdUsuario,
                identidad?.Usuario,
                context.Connection.RemoteIpAddress?.ToString(), context.TraceIdentifier, estado,
                consulta ? Numero(context.Request.Query["anio"].ToString()) : null,
                consulta ? Numero(context.Request.Query["mes"].ToString()) : null,
                consulta ? Formato(context.Request.Query["formato"].ToString()) : null,
                consulta && estado == 200 ? Numero(context.Response.Headers["X-Total-Registros"].ToString()) : null,
                (long)Stopwatch.GetElapsedTime(reloj).TotalMilliseconds);
            try
            {
                // Independiente de RequestAborted: conservar el intento aunque el cliente se desconecte.
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await repository.RegistrarAsync(evento, timeout.Token);
            }
            catch (Exception)
            {
                // Sin excepción SQL, cuerpos, query libre, contraseñas ni tokens en el log.
                logger.LogError("No se pudo guardar bitácora SQL. Evento {Evento}, estado {Estado}, TraceId {TraceId}.",
                    evento.Evento, estado, context.TraceIdentifier);
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Body = salida;
                await context.Response.WriteAsJsonAsync(new { mensaje = "No se pudo registrar la operación. Intente nuevamente.", traceId = context.TraceIdentifier });
                return;
            }
            context.Response.Body = salida;
            if (context.RequestAborted.IsCancellationRequested) return;
            buffer.Position = 0;
            await buffer.CopyToAsync(salida, context.RequestAborted);
        }
        finally { context.Response.Body = salida; }
    }

    private static int? Numero(string value) => int.TryParse(value, NumberStyles.Integer,
        CultureInfo.InvariantCulture, out var numero) ? numero : null;
    private static string? Formato(string value) => value switch
    {
        "" => "json", "json" => "json", "xlsx" => "xlsx", _ => null
    };
}

