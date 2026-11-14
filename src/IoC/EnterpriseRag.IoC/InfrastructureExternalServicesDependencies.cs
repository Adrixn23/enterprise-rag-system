namespace EnterpriseRag.IoC;

using EnterpriseRag.Core.Application.Contracts.Embeddings;
using EnterpriseRag.Core.Application.Contracts.FileService;
using EnterpriseRag.Core.Application.Contracts.GenerativeAi;
using EnterpriseRag.Core.Application.Contracts.TextExtraction;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.ExternalServices.Embeddings;
using EnterpriseRag.Infrastructure.ExternalServices.FileStorage;
using EnterpriseRag.Infrastructure.ExternalServices.GenerativeAi;
using EnterpriseRag.Infrastructure.ExternalServices.TextExtraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class InfrastructureExternalServicesDependencies
{
    public static IServiceCollection AddExternalServicesDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GeminiSettings>(configuration.GetSection(GeminiSettings.SectionName));

        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<ITextExtractorService, TextExtractorService>();

        services.AddHttpClient<IEmbeddingService, GeminiEmbeddingService>((serviceProvider, client) =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IGenerativeAiService, GeminiGenerativeAiService>((serviceProvider, client) =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(60); // Timeout adecuado para generacion LLM
        });

        return services;
    }
}
