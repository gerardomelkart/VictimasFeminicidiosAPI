using System.Text.Json.Serialization;

namespace VictimasFeminicidiosAPI.Models;

public sealed class FeminicidioRegistro
{
    [JsonIgnore]
    public int IdRegistro { get; init; }

    [JsonPropertyName("IdentificadorVictima"), JsonPropertyOrder(0)]
    public string? IdentificadorVictima { get; init; }

    [JsonPropertyName("Nombre"), JsonPropertyOrder(1)]
    public string? Nombre { get; init; }

    [JsonPropertyName("PrimerApellido"), JsonPropertyOrder(2)]
    public string? PrimerApellido { get; init; }

    [JsonPropertyName("SegundoApellido"), JsonPropertyOrder(3)]
    public string? SegundoApellido { get; init; }

    [JsonPropertyName("CURP"), JsonPropertyOrder(4)]
    public string? CURP { get; init; }

    [JsonPropertyName("TipoVictima"), JsonPropertyOrder(5)]
    public string? TipoVictima { get; init; }

    [JsonPropertyName("TipoPersonaMoral"), JsonPropertyOrder(6)]
    public string? TipoPersonaMoral { get; init; }

    [JsonPropertyName("Sexo"), JsonPropertyOrder(7)]
    public string? Sexo { get; init; }

    [JsonPropertyName("Genero"), JsonPropertyOrder(8)]
    public string? Genero { get; init; }

    [JsonPropertyName("PoblacionIndigena"), JsonPropertyOrder(9)]
    public string? PoblacionIndigena { get; init; }

    [JsonPropertyName("Discapacidad"), JsonPropertyOrder(10)]
    public string? Discapacidad { get; init; }

    [JsonPropertyName("FechaNacimiento"), JsonPropertyOrder(11)]
    public string? FechaNacimiento { get; init; }

    [JsonPropertyName("Edad"), JsonPropertyOrder(12)]
    public int? Edad { get; init; }

    [JsonPropertyName("Nacionalidad"), JsonPropertyOrder(13)]
    public string? Nacionalidad { get; init; }

    [JsonPropertyName("EntidadReportante"), JsonPropertyOrder(14)]
    public string? EntidadReportante { get; init; }

    [JsonPropertyName("UsuarioReportante"), JsonPropertyOrder(15)]
    public string? UsuarioReportante { get; init; }

    [JsonPropertyName("AnioCorte"), JsonPropertyOrder(16)]
    public int AnioCorte { get; init; }

    [JsonPropertyName("MesCorte"), JsonPropertyOrder(17)]
    public string? MesCorte { get; init; }

    [JsonPropertyName("IdentificadorCarpeta"), JsonPropertyOrder(18)]
    public string? IdentificadorCarpeta { get; init; }

    [JsonPropertyName("NumeroCarpeta"), JsonPropertyOrder(19)]
    public string? NumeroCarpeta { get; init; }

    [JsonPropertyName("FechaInicioCarpeta"), JsonPropertyOrder(20)]
    public string? FechaInicioCarpeta { get; init; }

    [JsonPropertyName("HoraInicioCarpeta"), JsonPropertyOrder(21)]
    public string? HoraInicioCarpeta { get; init; }

    [JsonPropertyName("ResumenHechos"), JsonPropertyOrder(22)]
    public string? ResumenHechos { get; init; }

    [JsonPropertyName("IdentificadorDelito"), JsonPropertyOrder(23)]
    public string? IdentificadorDelito { get; init; }

    [JsonPropertyName("DelitoFiscalia"), JsonPropertyOrder(24)]
    public string? DelitoFiscalia { get; init; }

    [JsonPropertyName("ModalidadFiscalia"), JsonPropertyOrder(25)]
    public string? ModalidadFiscalia { get; init; }

    [JsonPropertyName("BienJuridico"), JsonPropertyOrder(26)]
    public string? BienJuridico { get; init; }

    [JsonPropertyName("DelitoCatalogo"), JsonPropertyOrder(27)]
    public string? DelitoCatalogo { get; init; }

    [JsonPropertyName("SubtipoDelito"), JsonPropertyOrder(28)]
    public string? SubtipoDelito { get; init; }

    [JsonPropertyName("ModalidadCatalogo"), JsonPropertyOrder(29)]
    public string? ModalidadCatalogo { get; init; }

    [JsonPropertyName("FormaAccion"), JsonPropertyOrder(30)]
    public string? FormaAccion { get; init; }

    [JsonPropertyName("InstrumentoComision"), JsonPropertyOrder(31)]
    public string? InstrumentoComision { get; init; }

    [JsonPropertyName("GradoConsumacion"), JsonPropertyOrder(32)]
    public string? GradoConsumacion { get; init; }

    [JsonPropertyName("FechaHechos"), JsonPropertyOrder(33)]
    public string? FechaHechos { get; init; }

    [JsonPropertyName("HoraHechos"), JsonPropertyOrder(34)]
    public string? HoraHechos { get; init; }

    [JsonPropertyName("BienJuridicoSabana"), JsonPropertyOrder(35)]
    public string? BienJuridicoSabana { get; init; }

    [JsonPropertyName("DelitoSabana"), JsonPropertyOrder(36)]
    public string? DelitoSabana { get; init; }

    [JsonPropertyName("SubtipoDelitoSabana"), JsonPropertyOrder(37)]
    public string? SubtipoDelitoSabana { get; init; }

    [JsonPropertyName("ModalidadDelitoSabana"), JsonPropertyOrder(38)]
    public string? ModalidadDelitoSabana { get; init; }

    [JsonPropertyName("EntidadHechos"), JsonPropertyOrder(39)]
    public string? EntidadHechos { get; init; }

    [JsonPropertyName("MunicipioHechos"), JsonPropertyOrder(40)]
    public string? MunicipioHechos { get; init; }

    [JsonPropertyName("LocalidadHechos"), JsonPropertyOrder(41)]
    public string? LocalidadHechos { get; init; }

    [JsonPropertyName("ColoniaHechos"), JsonPropertyOrder(42)]
    public string? ColoniaHechos { get; init; }

    [JsonPropertyName("CodigoPostal"), JsonPropertyOrder(43)]
    public string? CodigoPostal { get; init; }

    [JsonPropertyName("DomicilioHechos"), JsonPropertyOrder(44)]
    public string? DomicilioHechos { get; init; }

    [JsonPropertyName("CoordenadaX"), JsonPropertyOrder(45)]
    public string? CoordenadaX { get; init; }

    [JsonPropertyName("CoordenadaY"), JsonPropertyOrder(46)]
    public string? CoordenadaY { get; init; }

    [JsonPropertyName("FechaRegistro"), JsonPropertyOrder(47)]
    public DateTime? FechaRegistro { get; init; }

    public static readonly IReadOnlyList<string> Columnas = Array.AsReadOnly(new[]
    {
        nameof(IdentificadorVictima),
        nameof(Nombre),
        nameof(PrimerApellido),
        nameof(SegundoApellido),
        nameof(CURP),
        nameof(TipoVictima),
        nameof(TipoPersonaMoral),
        nameof(Sexo),
        nameof(Genero),
        nameof(PoblacionIndigena),
        nameof(Discapacidad),
        nameof(FechaNacimiento),
        nameof(Edad),
        nameof(Nacionalidad),
        nameof(EntidadReportante),
        nameof(UsuarioReportante),
        nameof(AnioCorte),
        nameof(MesCorte),
        nameof(IdentificadorCarpeta),
        nameof(NumeroCarpeta),
        nameof(FechaInicioCarpeta),
        nameof(HoraInicioCarpeta),
        nameof(ResumenHechos),
        nameof(IdentificadorDelito),
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
        nameof(DelitoSabana),
        nameof(SubtipoDelitoSabana),
        nameof(ModalidadDelitoSabana),
        nameof(EntidadHechos),
        nameof(MunicipioHechos),
        nameof(LocalidadHechos),
        nameof(ColoniaHechos),
        nameof(CodigoPostal),
        nameof(DomicilioHechos),
        nameof(CoordenadaX),
        nameof(CoordenadaY),
        nameof(FechaRegistro),
    });
}
