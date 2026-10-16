using Dapper;
using Microsoft.Data.SqlClient;
using VictimasFeminicidiosAPI.Models;

namespace VictimasFeminicidiosAPI.Data;

public sealed class CuentaRepository(IConfiguration configuration) : ICuentaRepository
{
    private readonly string _connectionString = configuration.GetConnectionString("SiiidDb")
        ?? throw new InvalidOperationException("Configure ConnectionStrings:SiiidDb.");

    public Task<Cuenta?> BuscarAsync(string origen, string usuario, CancellationToken ct) =>
        BuscarInternoAsync(origen, usuario, null, ct);
    public Task<Cuenta?> BuscarPorIdAsync(string origen, int id, CancellationToken ct) =>
        BuscarInternoAsync(origen, null, id, ct);

    private async Task<Cuenta?> BuscarInternoAsync(string origen, string? usuario, int? id, CancellationToken ct)
    {
        // Nunca intentar la otra fuente si falla el origen elegido.
        var sql = origen switch
        {
            "EXTERNO" => """
                SELECT id_usuario AS Id, usuario AS Usuario, password_hash AS PasswordHash,
                       N'CONSULTA_FEMINICIDIOS' AS Rol, N'EXTERNO' AS Origen,
                       CONVERT(varchar(18), version, 1) AS Version
                FROM dbo.api_feminicidios_usuario
                WHERE habilitado = 1
                  AND ((@Id IS NULL AND usuario = @Usuario) OR id_usuario = @Id);
                """,
            "SIIID" => """
                SELECT u.id_usuario AS Id, u.usuario AS Usuario, u.password AS PasswordHash,
                       r.rol AS Rol, u.requiere_cambio_password AS RequiereCambioPassword,
                       N'SIIID' AS Origen
                FROM dbo.usuario u
                INNER JOIN dbo.roles r ON r.id_rol = u.id_rol AND r.activo = 1
                WHERE u.activo = 1 AND r.rol = N'SUPER_USUARIO'
                  AND ((@Id IS NULL AND u.usuario = @Usuario) OR u.id_usuario = @Id);
                """,
            _ => null
        };
        if (sql is null) return null;
        await using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<Cuenta>(
            new CommandDefinition(sql, new { Id = id, Usuario = usuario }, cancellationToken: ct));
    }

    public async Task<int?> CrearAsync(CrearUsuarioRequest request, string hash, int administrador, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO dbo.api_feminicidios_usuario
                (usuario, nombre, institucion, password_hash, id_usuario_creacion)
            OUTPUT INSERTED.id_usuario
            VALUES (@Usuario, @Nombre, @Institucion, @Hash, @Administrador);
            """;
        await using var connection = new SqlConnection(_connectionString);
        try
        {
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql,
                new { request.Usuario, request.Nombre, request.Institucion, Hash = hash, Administrador = administrador },
                cancellationToken: ct));
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627) { return null; }
    }

    public async Task<IReadOnlyList<UsuarioExterno>> ListarAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT id_usuario AS Id, usuario AS Usuario, nombre AS Nombre,
                   institucion AS Institucion, habilitado AS Habilitado
            FROM dbo.api_feminicidios_usuario ORDER BY usuario;
            """;
        await using var connection = new SqlConnection(_connectionString);
        return (await connection.QueryAsync<UsuarioExterno>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    public async Task<bool> HabilitarAsync(int id, bool habilitado, CancellationToken ct)
    {
        const string sql = """
            UPDATE dbo.api_feminicidios_usuario SET habilitado = @Habilitado,
                fecha_modificacion = SYSUTCDATETIME() WHERE id_usuario = @Id;
            """;
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, Habilitado = habilitado }, cancellationToken: ct)) == 1;
    }

    public async Task<bool> CambiarPasswordAsync(int id, string hash, CancellationToken ct)
    {
        const string sql = """
            UPDATE dbo.api_feminicidios_usuario SET password_hash = @Hash,
                fecha_modificacion = SYSUTCDATETIME() WHERE id_usuario = @Id;
            """;
        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, Hash = hash }, cancellationToken: ct)) == 1;
    }
}
