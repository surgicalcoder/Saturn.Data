using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using ServiceScan.SourceGenerator;

namespace GoLive.Saturn.Data;

public static partial class ServicesExtensions
{
    [GenerateServiceRegistrations(
        TypeNameFilter = "*Repository",   
        AsImplementedInterfaces = true,
        Lifetime = ServiceLifetime.Singleton)]
    public static partial IServiceCollection AddSaturnDataRepositoryServices(this IServiceCollection services);
}