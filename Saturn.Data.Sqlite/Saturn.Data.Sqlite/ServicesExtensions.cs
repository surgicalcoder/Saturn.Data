using Microsoft.Extensions.DependencyInjection;
using ServiceScan.SourceGenerator;

namespace Saturn.Data.Sqlite;

public static partial class ServicesExtensions
{
    [GenerateServiceRegistrations(
        TypeNameFilter = "*Repository",
        AsImplementedInterfaces = true,
        AsSelf = true,
        Lifetime = ServiceLifetime.Singleton)]
    public static partial IServiceCollection AddSaturnSqliteRepositoryServices(this IServiceCollection services);
}
