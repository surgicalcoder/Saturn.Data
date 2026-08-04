using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using LiteDbX;
using Microsoft.Extensions.DependencyInjection;

namespace Saturn.Data.LiteDbX.ChangeFeed;

public static class ChangeFeedServicesExtensions
{
    public static IServiceCollection AddLiteDbChangeFeed(this IServiceCollection services, LiteDatabase database, string source)
    {
        var sink = new LiteDbOutboxChangeFeedSink(database, source);
        services.AddSingleton<IChangeFeedSink>(sink);
        services.AddSingleton(new ChangeFeedPoller(sink));
        return services;
    }
}
