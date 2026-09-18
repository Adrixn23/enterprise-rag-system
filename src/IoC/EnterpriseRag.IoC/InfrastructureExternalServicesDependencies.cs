namespace EnterpriseRag.IoC;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class InfrastructureExternalServicesDependencies
{
    public static IServiceCollection AddExternalServicesDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services;
    }
}
