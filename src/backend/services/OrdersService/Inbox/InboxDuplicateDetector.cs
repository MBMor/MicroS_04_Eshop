using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace OrdersService.Inbox;

public static class InboxDuplicateDetector
{
    private const string ProcessedMessagesPrimaryKey = "PK_processed_messages";

    public static bool IsDuplicate(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ProcessedMessagesPrimaryKey
        };
    }

    public static bool IsTransient(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return FindNpgsqlException(exception) is { IsTransient: true };
    }

    private static NpgsqlException? FindNpgsqlException(Exception exception)
    {
        Exception? currentException = exception.InnerException;

        while (currentException is not null)
        {
            if (currentException is NpgsqlException npgsqlException)
            {
                return npgsqlException;
            }

            currentException = currentException.InnerException;
        }

        return null;
    }
}
