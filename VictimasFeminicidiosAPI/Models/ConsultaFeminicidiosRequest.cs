using System.ComponentModel.DataAnnotations;

namespace VictimasFeminicidiosAPI.Models;

public sealed class ConsultaFeminicidiosRequest
{
    [Required, Range(2000, 2100)]
    public int? Anio { get; init; }
    [Required, Range(1, 12)]
    public int? Mes { get; init; }
    [Required, RegularExpression("^(json|xlsx)$")]
    public string Formato { get; init; } = "json";
}
