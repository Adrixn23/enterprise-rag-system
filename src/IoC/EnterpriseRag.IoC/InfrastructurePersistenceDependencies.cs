namespace EnterpriseRag.IoC;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class InfrastructurePersistenceDependencies
{
    public static IServiceCollection AddInfrastructurePersistenceDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services;
    }
}
