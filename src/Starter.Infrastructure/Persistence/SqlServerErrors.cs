using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Starter.Infrastructure.Persistence;

internal static class SqlServerErrors
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };
}
