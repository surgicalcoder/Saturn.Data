using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Microsoft.Extensions.DependencyInjection;

namespace Saturn.Data.Sqlite.ChangeFeed;

public static class ChangeFeedServicesExtensions
{
    public static IServiceCollection AddSqliteChangeFeed(this IServiceCollection services, SqliteRepository repository, string source)
    {
        var sink = new SqliteOutboxChangeFeedSink(repository, source);
        services.AddSingleton<IChangeFeedSink>(sink);
        services.AddSingleton(new ChangeFeedPoller(sink));
        return services;
    }
}
