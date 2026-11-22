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
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

public static class InfrastructureExternalServicesDependencies
{
    public static IServiceCollection AddExternalServicesDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GeminiSettings>(configuration.GetSection(GeminiSettings.SectionName));
        services.Configure<FileStorageSettings>(configuration.GetSection(FileStorageSettings.SectionName));

        var storageProvider = configuration[$"{FileStorageSettings.SectionName}:Provider"];
        if (string.Equals(storageProvider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IFileStorageService, S3CompatibleFileStorageService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            });
        }
        else
        {
            services.AddScoped<IFileStorageService, LocalFileStorageService>();
        }

        services.AddScoped<ITextExtractorService, TextExtractorService>();

        services.AddHttpClient<IEmbeddingService, GeminiEmbeddingService>((serviceProvider, client) =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = Timeout.InfiniteTimeSpan; // Gestionado por el pipeline de resiliencia
        })
        .AddStandardResilienceHandler(options =>
        {
            ConfigureGeminiResilience(
                options,
                attemptTimeout: TimeSpan.FromSeconds(15),
                totalTimeout: TimeSpan.FromSeconds(60));
        });

        services.AddHttpClient<IGenerativeAiService, GeminiGenerativeAiService>((serviceProvider, client) =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = Timeout.InfiniteTimeSpan; // Gestionado por el pipeline de resiliencia
        })
        .AddStandardResilienceHandler(options =>
        {
            ConfigureGeminiResilience(
                options,
                attemptTimeout: TimeSpan.FromSeconds(60),
                totalTimeout: TimeSpan.FromSeconds(150));
        });

        services.AddSingleton<IPostConfigureOptions<HttpStandardResilienceOptions>>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>()
                .CreateLogger("EnterpriseRag.Resilience.Gemini");

            return new PostConfigureOptions<HttpStandardResilienceOptions>(name: null, options =>
            {
                var clientLabel = options.AttemptTimeout.Timeout >= TimeSpan.FromSeconds(60)
                    ? "generative-ai"
                    : "embeddings";

                options.Retry.OnRetry = args =>
                {
                    var statusCode = args.Outcome.Result is not null
                        ? ((int)args.Outcome.Result.StatusCode).ToString()
                        : args.Outcome.Exception?.GetType().Name ?? "Unknown";

                    logger.LogWarning(
                        "Reintento HTTP Gemini ({Client}) — intento {Attempt}/{MaxAttempts}, resultado {StatusCode}, espera {DelayMs} ms.",
                        clientLabel,
                        args.AttemptNumber + 1,
                        options.Retry.MaxRetryAttempts,
                        statusCode,
                        (int)args.RetryDelay.TotalMilliseconds);

                    return ValueTask.CompletedTask;
                };
            });
        });

        return services;
    }

    private static void ConfigureGeminiResilience(
        HttpStandardResilienceOptions options,
        TimeSpan attemptTimeout,
        TimeSpan totalTimeout)
    {
        // Máx 3 reintentos (4 intentos totales) con Delay inicial de 2s (~2s+4s+8s = ~14s ventana de recuperación,
        // ampliamente cubierta por TotalRequestTimeout de 60s/150s) y soporte nativo de Retry-After.
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(2);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;

        options.AttemptTimeout.Timeout = attemptTimeout;
        options.TotalRequestTimeout.Timeout = totalTimeout;

        // Circuit breaker: SamplingDuration debe ser al menos 2x AttemptTimeout (requisito de HttpStandardResilienceOptions)
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 10;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(Math.Max(30, attemptTimeout.TotalSeconds * 2));
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
    }
}
