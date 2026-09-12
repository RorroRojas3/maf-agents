using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Andes.Agents.Repository.Sql;

internal static class SqlErrors
{
    private const int _uniqueIndexViolation = 2601;
    private const int _uniqueConstraintViolation = 2627;
    private const int _timeout = -2;
    private const int _waitTimeout = 258;
    private const byte _resourceExhaustedSeverity = 17;
    private const byte _connectionFailureSeverity = 20;

    public static bool IsUniqueKeyViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: _uniqueIndexViolation or _uniqueConstraintViolation };

    // Severity 17 is a server out of log space, disk, memory or locks; 20 and above ends the connection. Both clear without a
    // code change, yet EF's retrying strategy counts few of them, and no timeout, as transient.
    public static bool IsStoreUnavailable(Exception exception) =>
        (exception as SqlException ?? exception.InnerException as SqlException) is { Class: _resourceExhaustedSeverity or >= _connectionFailureSeverity } or { Number: _timeout or _waitTimeout };
}
