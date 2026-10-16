using VictimasFeminicidiosAPI.Models;

namespace VictimasFeminicidiosAPI.Data;

public interface IBitacoraRepository
{
    Task RegistrarAsync(BitacoraEvento evento, CancellationToken ct);
}
