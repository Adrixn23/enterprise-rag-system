namespace EnterpriseRag.IoC;

using EnterpriseRag.Core.Application.Contracts.FileService;
using EnterpriseRag.Core.Application.Contracts.TextExtraction;
using EnterpriseRag.Infrastructure.ExternalServices.FileStorage;
using EnterpriseRag.Infrastructure.ExternalServices.TextExtraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class InfrastructureExternalServicesDependencies
{
    public static IServiceCollection AddExternalServicesDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<ITextExtractorService, TextExtractorService>();

        return services;
    }
}
