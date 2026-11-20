using System.Data;

namespace ChecadorAPI.Data;

public interface ISqlConnectionFactory
{
    IDbConnection CreateConnection();
}
