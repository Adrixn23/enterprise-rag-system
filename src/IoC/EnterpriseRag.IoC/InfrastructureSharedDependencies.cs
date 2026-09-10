namespace EnterpriseRag.IoC;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class InfrastructureSharedDependencies
{
    public static IServiceCollection AddInfrastructureSharedDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services;
    }
}
