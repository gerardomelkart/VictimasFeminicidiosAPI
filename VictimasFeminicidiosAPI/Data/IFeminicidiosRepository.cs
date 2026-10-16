using VictimasFeminicidiosAPI.Models;

namespace VictimasFeminicidiosAPI.Data;

public interface IFeminicidiosRepository
{
    Task<IReadOnlyList<FeminicidioRegistro>> ConsultarAsync(int anio, int mes, CancellationToken ct);
}
