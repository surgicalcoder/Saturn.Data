using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

internal static class SqliteRetry
{
    private const int Busy = 5;
    private const int Locked = 6;

    public static async Task<int> ExecuteNonQueryWithRetryAsync(this SqliteCommand command, SqliteRepositoryOptions options,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException exception) when (IsRetryable(exception) && attempt < options.BusyRetryCount)
            {
                await DelayAsync(attempt, options, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static async Task<object?> ExecuteScalarWithRetryAsync(this SqliteCommand command, SqliteRepositoryOptions options,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException exception) when (IsRetryable(exception) && attempt < options.BusyRetryCount)
            {
                await DelayAsync(attempt, options, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsRetryable(SqliteException exception)
        => exception.SqliteErrorCode is Busy or Locked;

    private static Task DelayAsync(int attempt, SqliteRepositoryOptions options, CancellationToken cancellationToken)
    {
        var delay = options.BusyRetryBaseDelayMs * (int)Math.Pow(2, attempt);
        return Task.Delay(delay, cancellationToken);
    }
}
