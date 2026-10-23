using Dapper;
using Microsoft.Data.SqlClient;
using VictimasFeminicidiosAPI.Models;

namespace VictimasFeminicidiosAPI.Data;

public sealed class BitacoraRepository(IConfiguration configuration) : IBitacoraRepository
{
    private readonly string _connectionString = configuration.GetConnectionString("SiiidDb")
        ?? throw new InvalidOperationException("Configure ConnectionStrings:SiiidDb.");

    public async Task RegistrarAsync(BitacoraEvento evento, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO dbo.api_feminicidios_bitacora
                (fecha_hora_mexico, evento, origen, id_usuario, usuario, ip, trace_id, estado_http,
                 anio, mes, formato, total_registros, duracion_ms)
            VALUES (@FechaHoraMexico, @Evento, @Origen, @IdUsuario, @Usuario, @Ip, @TraceId,
                    @EstadoHttp, @Anio, @Mes, @Formato, @TotalRegistros, @DuracionMs);
            """;
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(new CommandDefinition(sql, evento, commandTimeout: 5, cancellationToken: ct));
    }
}

