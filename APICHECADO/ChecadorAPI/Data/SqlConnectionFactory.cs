using System.Data;
using Microsoft.Data.SqlClient;

namespace ChecadorAPI.Data;

public class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("ChecadorDb")
            ?? throw new InvalidOperationException("Cadena de conexion 'ChecadorDb' no configurada.");
    }

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
