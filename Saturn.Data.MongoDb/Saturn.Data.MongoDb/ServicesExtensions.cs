using Microsoft.Extensions.DependencyInjection;
using ServiceScan.SourceGenerator;

namespace Saturn.Data.MongoDb;

public static partial class ServicesExtensions
{
    [GenerateServiceRegistrations(
        TypeNameFilter = "*Repository",
        AsImplementedInterfaces = true,
        AsSelf = true,
        Lifetime = ServiceLifetime.Singleton)]
    public static partial IServiceCollection AddSaturnDataRepositoryServices(this IServiceCollection services);
}