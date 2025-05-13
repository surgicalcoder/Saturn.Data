using Microsoft.Extensions.DependencyInjection;
using ServiceScan.SourceGenerator;

namespace Saturn.Data.LiteDb;

public static partial class ServicesExtensions
{
    [GenerateServiceRegistrations(
        TypeNameFilter = "*Repository",
        AsImplementedInterfaces = true,
        Lifetime = ServiceLifetime.Singleton)]
    public static partial IServiceCollection AddSaturnLiteDBRepositoryServices(this IServiceCollection services);
}