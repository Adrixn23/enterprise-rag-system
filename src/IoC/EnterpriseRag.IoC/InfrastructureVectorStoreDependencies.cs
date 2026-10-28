namespace EnterpriseRag.IoC;

using EnterpriseRag.Core.Application.Contracts.VectorStore;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.VectorStore.VectorStoreService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qdrant.Client;

public static class InfrastructureVectorStoreDependencies
{
    public static IServiceCollection AddVectorStoreDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<QdrantSettings>(configuration.GetSection(QdrantSettings.SectionName));

        services.AddSingleton<QdrantClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<QdrantSettings>>().Value;
            return new QdrantClient(
                host: settings.Host,
                port: settings.Port,
                apiKey: settings.ApiKey
            );
        });

        services.AddScoped<IVectorStoreService, QdrantVectorStoreService>();

        return services;
    }
}
