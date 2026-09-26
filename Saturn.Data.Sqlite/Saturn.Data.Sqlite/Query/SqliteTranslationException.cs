namespace Saturn.Data.Sqlite.Query;

public sealed class SqliteTranslationException : Exception
{
    public SqliteTranslationException(string message) : base(message)
    {
    }

    public SqliteTranslationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
