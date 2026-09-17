using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Wayd.Infrastructure.Persistence.Extensions;

public static class DbUpdateExceptionExtensions
{
    // SQL Server: 2627 = unique constraint, 2601 = unique index.
    private const int SqlServerUniqueConstraint = 2627;
    private const int SqlServerUniqueIndex = 2601;

    /// <summary>
    /// Whether the save failed because a row with the same unique key already exists — for a caller whose
    /// goal is "ensure this row exists", the sign that a concurrent caller got there first.
    /// </summary>
    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is SqlException sql
        && sql.Number is SqlServerUniqueConstraint or SqlServerUniqueIndex;
}
