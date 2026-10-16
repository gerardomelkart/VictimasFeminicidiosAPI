namespace VictimasFeminicidiosAPI.Models;

public sealed record BitacoraIdentidad(string Origen, int? IdUsuario, string Usuario);

public sealed record BitacoraEvento(DateTime FechaUtc, string Evento, string? Origen,
    int? IdUsuario, string? Usuario, string? Ip, string TraceId, int EstadoHttp,
    int? Anio, int? Mes, string? Formato, int? TotalRegistros, long DuracionMs);
