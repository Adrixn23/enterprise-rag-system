using EnterpriseRag.Core.Application.Contracts.Chat;
using EnterpriseRag.Core.Application.Contracts.Documents;
using EnterpriseRag.Core.Application.Contracts.Search;
using EnterpriseRag.Core.Application.Contracts.TextChunker;
using EnterpriseRag.Core.Application.Mapping.Documents;
using EnterpriseRag.Core.Application.Services.Chat;
using EnterpriseRag.Core.Application.Services.Documents;
using EnterpriseRag.Core.Application.Services.Search;
using EnterpriseRag.Core.Application.Services.TextChunker;
using Mapster;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseRag.IoC;

public static class ApplicationDependencies
{
    public static IServiceCollection AddApplicationDependencies(this IServiceCollection services)
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(DocumentMappingConfig).Assembly);

        services.AddScoped<ITextChunkerService, TextChunkerService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}
