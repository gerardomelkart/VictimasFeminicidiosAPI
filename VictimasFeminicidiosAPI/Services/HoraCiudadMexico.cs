namespace VictimasFeminicidiosAPI.Services;

public static class HoraCiudadMexico
{
    // Zona explicita: no depender de la zona local del servidor Linux o Windows.
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    public static DateTime ConvertirDesdeUtc(DateTime fechaUtc) =>
        TimeZoneInfo.ConvertTimeFromUtc(fechaUtc, Zona);
}
