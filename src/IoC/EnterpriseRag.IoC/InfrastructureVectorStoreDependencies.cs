namespace EnterpriseRag.IoC;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class InfrastructureVectorStoreDependencies
{
    public static IServiceCollection AddVectorStoreDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services;
    }
}
