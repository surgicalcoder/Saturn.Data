using Microsoft.Data.Sqlite;

namespace Saturn.Data.Sqlite.Query;

public sealed class SqlFragment
{
    public string Sql { get; init; } = "1=1";

    public IReadOnlyList<SqliteParameter> Parameters { get; init; } = Array.Empty<SqliteParameter>();

    public static SqlFragment AlwaysTrue { get; } = new() { Sql = "1=1" };

    public static SqlFragment AlwaysFalse { get; } = new() { Sql = "1=0" };

    public static SqlFragment Combine(SqlFragment left, SqlFragment right, string op)
        => new()
        {
            Sql = $"({left.Sql}) {op} ({right.Sql})",
            Parameters = left.Parameters.Concat(right.Parameters).ToList()
        };
}
