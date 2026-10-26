using System.Text.Json.Serialization;

namespace VictimasFeminicidiosAPI.Models;

public sealed class FeminicidioRegistro
{
    [JsonIgnore]
    public int IdRegistro { get; init; }

    [JsonPropertyName("Nombre"), JsonPropertyOrder(0)]
    public string? Nombre { get; init; }

    [JsonPropertyName("PrimerApellido"), JsonPropertyOrder(1)]
    public string? PrimerApellido { get; init; }

    [JsonPropertyName("SegundoApellido"), JsonPropertyOrder(2)]
    public string? SegundoApellido { get; init; }

    [JsonPropertyName("CURP"), JsonPropertyOrder(3)]
    public string? CURP { get; init; }

    [JsonPropertyName("Sexo"), JsonPropertyOrder(4)]
    public string? Sexo { get; init; }

    [JsonPropertyName("Genero"), JsonPropertyOrder(5)]
    public string? Genero { get; init; }

    [JsonPropertyName("FechaNacimiento"), JsonPropertyOrder(6)]
    public string? FechaNacimiento { get; init; }

    [JsonPropertyName("Edad"), JsonPropertyOrder(7)]
    public int? Edad { get; init; }

    [JsonPropertyName("Nacionalidad"), JsonPropertyOrder(8)]
    public string? Nacionalidad { get; init; }

    [JsonPropertyName("EntidadReportante"), JsonPropertyOrder(9)]
    public string? EntidadReportante { get; init; }

    [JsonPropertyName("NumeroCarpeta"), JsonPropertyOrder(10)]
    public string? NumeroCarpeta { get; init; }

    [JsonPropertyName("FechaInicioCarpeta"), JsonPropertyOrder(11)]
    public string? FechaInicioCarpeta { get; init; }

    [JsonPropertyName("HoraInicioCarpeta"), JsonPropertyOrder(12)]
    public string? HoraInicioCarpeta { get; init; }

    [JsonPropertyName("DelitoFiscalia"), JsonPropertyOrder(13)]
    public string? DelitoFiscalia { get; init; }

    [JsonPropertyName("ModalidadFiscalia"), JsonPropertyOrder(14)]
    public string? ModalidadFiscalia { get; init; }

    [JsonPropertyName("BienJuridico"), JsonPropertyOrder(15)]
    public string? BienJuridico { get; init; }

    [JsonPropertyName("DelitoCatalogo"), JsonPropertyOrder(16)]
    public string? DelitoCatalogo { get; init; }

    [JsonPropertyName("SubtipoDelito"), JsonPropertyOrder(17)]
    public string? SubtipoDelito { get; init; }

    [JsonPropertyName("ModalidadCatalogo"), JsonPropertyOrder(18)]
    public string? ModalidadCatalogo { get; init; }

    [JsonPropertyName("FormaAccion"), JsonPropertyOrder(19)]
    public string? FormaAccion { get; init; }

    [JsonPropertyName("InstrumentoComision"), JsonPropertyOrder(20)]
    public string? InstrumentoComision { get; init; }

    [JsonPropertyName("GradoConsumacion"), JsonPropertyOrder(21)]
    public string? GradoConsumacion { get; init; }

    [JsonPropertyName("FechaHechos"), JsonPropertyOrder(22)]
    public string? FechaHechos { get; init; }

    [JsonPropertyName("HoraHechos"), JsonPropertyOrder(23)]
    public string? HoraHechos { get; init; }

    [JsonPropertyName("BienJuridicoSabana"), JsonPropertyOrder(24)]
    public string? BienJuridicoSabana { get; init; }

    [JsonPropertyName("EntidadHechos"), JsonPropertyOrder(25)]
    public string? EntidadHechos { get; init; }

    [JsonPropertyName("MunicipioHechos"), JsonPropertyOrder(26)]
    public string? MunicipioHechos { get; init; }

    public static readonly IReadOnlyList<string> Columnas = Array.AsReadOnly(new[]
    {
        nameof(Nombre),
        nameof(PrimerApellido),
        nameof(SegundoApellido),
        nameof(CURP),
        nameof(Sexo),
        nameof(Genero),
        nameof(FechaNacimiento),
        nameof(Edad),
        nameof(Nacionalidad),
        nameof(EntidadReportante),
        nameof(NumeroCarpeta),
        nameof(FechaInicioCarpeta),
        nameof(HoraInicioCarpeta),
        nameof(DelitoFiscalia),
        nameof(ModalidadFiscalia),
        nameof(BienJuridico),
        nameof(DelitoCatalogo),
        nameof(SubtipoDelito),
        nameof(ModalidadCatalogo),
        nameof(FormaAccion),
        nameof(InstrumentoComision),
        nameof(GradoConsumacion),
        nameof(FechaHechos),
        nameof(HoraHechos),
        nameof(BienJuridicoSabana),
        nameof(EntidadHechos),
        nameof(MunicipioHechos),
    });
}
