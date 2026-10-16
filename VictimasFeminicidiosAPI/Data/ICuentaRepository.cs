using VictimasFeminicidiosAPI.Models;

namespace VictimasFeminicidiosAPI.Data;

public interface ICuentaRepository
{
    Task<Cuenta?> BuscarAsync(string origen, string usuario, CancellationToken ct);
    Task<Cuenta?> BuscarPorIdAsync(string origen, int id, CancellationToken ct);
    Task<int?> CrearAsync(CrearUsuarioRequest request, string hash, int administrador, CancellationToken ct);
    Task<IReadOnlyList<UsuarioExterno>> ListarAsync(CancellationToken ct);
    Task<bool> HabilitarAsync(int id, bool habilitado, CancellationToken ct);
    Task<bool> CambiarPasswordAsync(int id, string hash, CancellationToken ct);
}
