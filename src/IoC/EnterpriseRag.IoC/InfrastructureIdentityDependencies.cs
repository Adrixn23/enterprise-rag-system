namespace EnterpriseRag.IoC;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class InfrastructureIdentityDependencies
{
    public static IServiceCollection AddInfrastructureIdentityDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services;
    }
}
