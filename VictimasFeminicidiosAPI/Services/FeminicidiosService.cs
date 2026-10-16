using VictimasFeminicidiosAPI.Data;
using VictimasFeminicidiosAPI.Models;

namespace VictimasFeminicidiosAPI.Services;

public sealed class FeminicidiosService(IFeminicidiosRepository repository)
{
    public async Task<IReadOnlyList<FeminicidioRegistro>> ConsultarAsync(int anio, int mes, CancellationToken ct)
    {
        var registros = await repository.ConsultarAsync(anio, mes, ct);
        if (registros.Select(r => r.IdRegistro).Distinct().Count() != registros.Count)
            throw new InvalidOperationException("La clasificación de sábana produce más de una fila por registro de víctima. Revisar el catálogo antes de exportar.");
        return registros;
    }
}
