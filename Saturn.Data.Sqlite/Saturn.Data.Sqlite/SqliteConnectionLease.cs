using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite;

internal sealed class SqliteConnectionLease : IAsyncDisposable
{
    private readonly bool ownsConnection;

    public SqliteConnectionLease(SqliteConnection connection, bool ownsConnection)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.ownsConnection = ownsConnection;
    }

    public SqliteConnection Connection { get; }

    public async ValueTask DisposeAsync()
    {
        if (ownsConnection)
        {
            await Connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
