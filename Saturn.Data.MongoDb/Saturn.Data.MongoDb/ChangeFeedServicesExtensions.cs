using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Saturn.Data.MongoDb;

public static class ChangeFeedServicesExtensions
{
    public static IServiceCollection AddMongoChangeFeed(this IServiceCollection services, IMongoDatabase database, string source)
    {
        var sink = new MongoOutboxChangeFeedSink(database, source);
        services.AddSingleton<IChangeFeedSink>(sink);
        services.AddSingleton(new ChangeFeedPoller(sink));
        return services;
    }
}
