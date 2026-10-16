using Dapper;
using Microsoft.Data.SqlClient;
using VictimasFeminicidiosAPI.Models;

namespace VictimasFeminicidiosAPI.Data;

public sealed class FeminicidiosRepository(IConfiguration configuration) : IFeminicidiosRepository
{
    public async Task<IReadOnlyList<FeminicidioRegistro>> ConsultarAsync(int anio, int mes, CancellationToken ct)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("SiiidDb"));
        return (await connection.QueryAsync<FeminicidioRegistro>(new CommandDefinition(
            FeminicidiosSql.Consulta, new { Anio = anio, Mes = mes }, commandTimeout: 120, cancellationToken: ct))).AsList();
    }
}
