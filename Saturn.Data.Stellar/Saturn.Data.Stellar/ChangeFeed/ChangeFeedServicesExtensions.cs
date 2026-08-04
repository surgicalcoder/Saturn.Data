using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Microsoft.Extensions.DependencyInjection;
using Stellar.Collections;

namespace Saturn.Data.Stellar.ChangeFeed;

public static class ChangeFeedServicesExtensions
{
    public static IServiceCollection AddStellarChangeFeed(this IServiceCollection services, FastDB database, string source)
    {
        var sink = new StellarOutboxChangeFeedSink(database, source);
        services.AddSingleton<IChangeFeedSink>(sink);
        services.AddSingleton(new ChangeFeedPoller(sink));
        return services;
    }
}
