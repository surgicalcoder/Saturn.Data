using GoLive.Saturn.Data.Abstractions.ChangeFeed;
using Microsoft.Extensions.DependencyInjection;

namespace Saturn.Data.DocumentDb.ChangeFeed;

public static class ChangeFeedServicesExtensions
{
    public static IServiceCollection AddDocumentDbChangeFeed(this IServiceCollection services, DocumentDbRepository repository, string source)
    {
        var sink = new DocumentDbOutboxChangeFeedSink(repository, source);
        services.AddSingleton<IChangeFeedSink>(sink);
        services.AddSingleton(new ChangeFeedPoller(sink));
        return services;
    }
}
