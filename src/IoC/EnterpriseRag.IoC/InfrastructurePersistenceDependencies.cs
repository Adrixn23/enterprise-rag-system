namespace EnterpriseRag.IoC;

using EnterpriseRag.Core.Domain.Interfaces.Documents;
using EnterpriseRag.Core.Domain.Interfaces.GenericRepository;
using EnterpriseRag.Core.Domain.Interfaces.Outbox;
using EnterpriseRag.Infrastructure.Persistence.Context;
using EnterpriseRag.Infrastructure.Persistence.Repositories.Documents;
using EnterpriseRag.Infrastructure.Persistence.Repositories.GenericRepository;
using EnterpriseRag.Infrastructure.Persistence.Repositories.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class InfrastructurePersistenceDependencies
{
    public static IServiceCollection AddInfrastructurePersistenceDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b =>
                {
                    b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                    b.CommandTimeout(30);
                }));

        services.AddScoped(typeof(IGenericRepository<,>), typeof(GenericRepository<,>));
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        return services;
    }
}
